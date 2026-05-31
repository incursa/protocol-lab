#!/usr/bin/env python3
"""Minimal R2 S3 helper for ProtocolLab publication.

This script uploads and downloads single objects through Cloudflare's S3
compatible R2 endpoint. It is intentionally small so the PowerShell publisher
can keep the validation logic while delegating only the object I/O.
"""

import os
import sys
from pathlib import Path
from typing import Optional, Sequence

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


def _put(bucket: str, key: str, file_path: str, content_type: Optional[str], cache_control: Optional[str]) -> int:
    client = _client()
    kwargs = {"Bucket": bucket, "Key": key}
    if content_type:
        kwargs["ContentType"] = content_type
    if cache_control:
        kwargs["CacheControl"] = cache_control

    with open(file_path, "rb") as body:
        kwargs["Body"] = body
        client.put_object(**kwargs)

    return 0


def _get(bucket: str, key: str, file_path: str) -> int:
    client = _client()
    try:
        response = client.get_object(Bucket=bucket, Key=key)
    except ClientError as exc:
        error = exc.response.get("Error", {})
        code = str(error.get("Code", ""))
        status = exc.response.get("ResponseMetadata", {}).get("HTTPStatusCode")
        if code in {"NoSuchKey", "NotFound", "404"} or status == 404:
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
        error = exc.response.get("Error", {})
        code = str(error.get("Code", ""))
        status = exc.response.get("ResponseMetadata", {}).get("HTTPStatusCode")
        if code in {"NoSuchKey", "NotFound", "404"} or status == 404:
            return 2
        raise

    return 0


def main(argv: Sequence[str]) -> int:
    if len(argv) < 5:
        raise SystemExit(
            "Usage: r2_s3_helper.py <put|get|head> <bucket> <key> <file> [content_type] [cache_control]"
        )

    operation = argv[1]
    bucket = argv[2]
    key = argv[3]
    file_path = argv[4]
    content_type = argv[5] if len(argv) > 5 else None
    cache_control = argv[6] if len(argv) > 6 else None

    if operation == "put":
        return _put(bucket, key, file_path, content_type, cache_control)

    if operation == "get":
        return _get(bucket, key, file_path)

    if operation == "head":
        return _head(bucket, key)

    raise SystemExit(f"Unsupported operation: {operation}")


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
