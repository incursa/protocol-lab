#!/usr/bin/env sh
set -eu

TAG="${TAG:-incursa/protocol-lab-h2load-http3:local}"
DOCKERFILE="${DOCKERFILE:-tools/h2load-http3/Dockerfile}"
REPO_ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"

docker build --pull --tag "$TAG" --file "$REPO_ROOT/$DOCKERFILE" "$REPO_ROOT"
docker run --rm "$TAG" h2load --version

HELP="$(docker run --rm --entrypoint sh "$TAG" -c "h2load --help")"
for option in --h3 --output-file --qlog-file-base --connect-to --sni; do
  printf "%s" "$HELP" | grep -q -- "$option"
done

printf "h2load HTTP/3 image proof passed for %s\n" "$TAG"
