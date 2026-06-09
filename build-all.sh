#!/usr/bin/env sh
set -eu

if [ "$#" -ne 1 ]; then
  echo "Usage: ./build-all.sh <docker-image-tag>" >&2
  exit 1
fi

TAG="$1"
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

"$SCRIPT_DIR/build-dashboard.bat" "$TAG"
"$SCRIPT_DIR/build-server.bat" "$TAG"
