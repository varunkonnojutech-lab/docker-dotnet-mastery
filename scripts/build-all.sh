#!/usr/bin/env bash
# Build all three image variants, then print their sizes side by side.
# Needs Docker. Run from the repo root.
set -euo pipefail
cd "$(dirname "$0")/.."

docker build -t docker-demo:prod -f Dockerfile .
docker build -t docker-demo:naive -f Dockerfile.naive .
docker build -t docker-demo:distroless -f Dockerfile.distroless .

echo
echo "Image sizes (expect: naive >> prod > distroless):"
docker images --format '{{.Repository}}:{{.Tag}}  {{.Size}}' | grep '^docker-demo:'
