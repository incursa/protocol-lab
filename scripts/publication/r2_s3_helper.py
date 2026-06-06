#!/usr/bin/env python3
"""R2 S3 helper for ProtocolLab publication bundles.

The PowerShell uploader owns ProtocolLab validation and object-key planning.
This helper owns only S3-compatible R2 object I/O so an entire bundle can be
uploaded and verified in one Python process.
"""

from __future__ import annotations

import concurrent.futures
import json
import mimetypes
import os
import sys
from pathlib import Path
from typing import Any, Mapping, Sequence

import boto3
from botocore.exceptions import ClientError


def _client():
    endpoint = os.environ.get("R2_ENDPOINT")
    if not endpoint:
        account_id = os.environ.get("CLOUDFLARE_ACCOUNT_ID")
        if account_id:
            endpoint = f"https://{account_id}.r2.cloudflarestorage.com"
    if not endpoint:
        raise SystemExit("R2_ENDPOINT or CLOUDFLARE_ACCOUNT_ID is required.")

    access_key_id = os.environ.get("AWS_ACCESS_KEY_ID")
    secret_access_key = os.environ.get("AWS_SECRET_ACCESS_KEY")
    if not access_key_id or not secret_access_key:
        raise SystemExit("AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are required.")

    return boto3.client(
        "s3",
        region_name=os.environ.get("AWS_DEFAULT_REGION", "auto"),
        endpoint_url=endpoint.rstrip("/"),
        aws_access_key_id=access_key_id,
        aws_secret_access_key=secret_access_key,
        aws_session_token=os.environ.get("AWS_SESSION_TOKEN") or None,
    )


def _content_type(path: str, explicit: str | None) -> str | None:
    if explicit:
        return explicit
    guessed, _ = mimetypes.guess_type(path)
    if guessed:
        return guessed
    suffix = Path(path).suffix.lower()
    if suffix == ".json":
        return "application/json"
    if suffix in {".md", ".txt", ".log"}:
        return "text/plain; charset=utf-8"
    return "application/octet-stream"


def _put_object(client, bucket: str, item: Mapping[str, Any]) -> dict[str, Any]:
    source = str(item["source"])
    key = str(item["key"])
    kwargs: dict[str, Any] = {
        "Bucket": bucket,
        "Key": key,
        "ContentType": _content_type(source, item.get("contentType")),
    }
    cache_control = item.get("cacheControl")
    if cache_control:
        kwargs["CacheControl"] = str(cache_control)

    with open(source, "rb") as body:
        kwargs["Body"] = body
        client.put_object(**kwargs)

    return {"key": key, "source": source, "status": "uploaded"}


def _is_missing(exc: ClientError) -> bool:
    error = exc.response.get("Error", {})
    code = str(error.get("Code", ""))
    status = exc.response.get("ResponseMetadata", {}).get("HTTPStatusCode")
    return code in {"NoSuchKey", "NotFound", "404"} or status == 404


def _verify_object(client, bucket: str, item: Mapping[str, Any]) -> dict[str, Any]:
    key = str(item["key"])
    try:
        client.head_object(Bucket=bucket, Key=key)
    except ClientError as exc:
        if _is_missing(exc):
            raise RuntimeError(f"missing R2 object: {key}") from exc
        raise

    verify_json = bool(item.get("verifyJson"))
    if verify_json:
        try:
            response = client.get_object(Bucket=bucket, Key=key)
        except ClientError as exc:
            if _is_missing(exc):
                raise RuntimeError(f"missing R2 JSON object: {key}") from exc
            raise

        payload = response["Body"].read()
        try:
            decoded = json.loads(payload.decode("utf-8"))
        except Exception as exc:  # noqa: BLE001 - surface exact object key.
            raise RuntimeError(f"malformed JSON R2 object: {key}") from exc

        expected_run_id = item.get("expectedRunId")
        if expected_run_id and isinstance(decoded, dict):
            actual_run_id = decoded.get("runId")
            if actual_run_id and str(actual_run_id).lower() != str(expected_run_id).lower():
                raise RuntimeError(
                    f"R2 object {key} has runId {actual_run_id!r}, expected {expected_run_id!r}"
                )

    return {"key": key, "status": "verified"}


def _run_parallel(label: str, concurrency: int, work_items: Sequence[Mapping[str, Any]], fn):
    results: list[dict[str, Any]] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, concurrency)) as executor:
        futures = [executor.submit(fn, item) for item in work_items]
        for future in concurrent.futures.as_completed(futures):
            try:
                results.append(future.result())
            except Exception as exc:  # noqa: BLE001 - keep helper output compact.
                raise SystemExit(f"{label} failed: {exc}") from exc
    return results


def _upload_manifest(manifest_path: str) -> int:
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    bucket = str(manifest["bucket"])
    entries = list(manifest.get("entries", []))
    concurrency = int(manifest.get("concurrency", 8))
    verify = bool(manifest.get("verify", False))
    client = _client()

    def put(item):
        return _put_object(client, bucket, item)

    uploaded = _run_parallel("upload", concurrency, entries, put)

    verified: list[dict[str, Any]] = []
    if verify:
        def head(item):
            return _verify_object(client, bucket, item)

        verified = _run_parallel("verification", concurrency, entries, head)

    output = {
        "bucket": bucket,
        "uploadedCount": len(uploaded),
        "verifiedCount": len(verified),
    }
    print(json.dumps(output, sort_keys=True))
    return 0


def _get(bucket: str, key: str, file_path: str) -> int:
    client = _client()
    try:
        response = client.get_object(Bucket=bucket, Key=key)
    except ClientError as exc:
        if _is_missing(exc):
            return 2
        raise

    body = response["Body"].read()
    Path(file_path).write_bytes(body)
    return 0


def _head(bucket: str, key: str) -> int:
    client = _client()
    try:
        client.head_object(Bucket=bucket, Key=key)
    except ClientError as exc:
        if _is_missing(exc):
            return 2
        raise

    return 0


def main(argv: Sequence[str]) -> int:
    if len(argv) < 2:
        raise SystemExit("Usage: r2_s3_helper.py upload-manifest <manifest.json> | get <bucket> <key> <file> | head <bucket> <key>")

    operation = argv[1]
    if operation == "upload-manifest":
        if len(argv) != 3:
            raise SystemExit("Usage: r2_s3_helper.py upload-manifest <manifest.json>")
        return _upload_manifest(argv[2])

    if operation == "get":
        if len(argv) != 5:
            raise SystemExit("Usage: r2_s3_helper.py get <bucket> <key> <file>")
        return _get(argv[2], argv[3], argv[4])

    if operation == "head":
        if len(argv) != 4:
            raise SystemExit("Usage: r2_s3_helper.py head <bucket> <key>")
        return _head(argv[2], argv[3])

    raise SystemExit(f"Unsupported operation: {operation}")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
