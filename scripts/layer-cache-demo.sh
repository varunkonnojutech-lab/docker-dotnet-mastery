#!/usr/bin/env bash
# Demonstrate layer caching: build twice, then change one code file and
# build again. Watch the second build reuse the restore layer ("CACHED")
# and only rebuild the publish layer after the code change.
# Needs Docker. Run from the repo root.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "=== Build 1: everything is fresh ==="
docker build -t docker-demo:prod -f Dockerfile .
echo
echo "=== Build 2: nothing changed — watch for CACHED steps ==="
docker build -t docker-demo:prod -f Dockerfile .
echo
echo "=== Now edit a comment in src/DockerDemo.Api/Program.cs and re-run ==="
echo "=== Only the COPY + publish steps rebuild; restore stays CACHED. ==="
