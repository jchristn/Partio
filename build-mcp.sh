#!/usr/bin/env sh
set -eu

TAG="${1:-latest}"
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"

IMAGE=jchristn77/partio-mcp
if [ "$TAG" = "latest" ]; then
  set -- -t "$IMAGE:latest"
else
  set -- -t "$IMAGE:$TAG" -t "$IMAGE:latest"
fi

echo "=== Building $IMAGE:$TAG multi-arch and pushing to Docker Hub ==="
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 "$@" -f src/Partio.McpServer/Dockerfile --push .

echo "=== Pulling $IMAGE:$TAG into the local Docker daemon ==="
docker pull "$IMAGE:$TAG"
