#!/usr/bin/env bash
#
# reset.sh - Reset the Partio Docker deployment to factory defaults.
#
# This script:
#   1. Prompts the user to confirm by typing RESET
#   2. Runs docker compose down to stop and remove containers
#   3. Deletes transient files (logs, request history)
#   4. Restores the factory database and configuration
#

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo ""
echo "========================================="
echo "  Partio Factory Reset"
echo "========================================="
echo ""
echo "This will:"
echo "  - Stop and remove all Partio containers"
echo "  - Delete all log files"
echo "  - Delete all request history files"
echo "  - Replace the database with a clean factory copy"
echo "  - Replace partio.json with the factory default"
echo ""
echo "WARNING: All current data will be lost!"
echo ""
read -rp "Type RESET to confirm: " confirmation

if [ "$confirmation" != "RESET" ]; then
    echo ""
    echo "Reset cancelled."
    exit 1
fi

echo ""
echo "Stopping containers..."
cd "$DOCKER_DIR"
docker compose down 2>/dev/null || true

echo "Removing log files..."
find "$DOCKER_DIR/logs" -type f -name "*.log*" -delete 2>/dev/null || true
find "$DOCKER_DIR/logs" -mindepth 1 -type d -exec rm -rf {} + 2>/dev/null || true

echo "Removing request history files..."
find "$DOCKER_DIR/request-history" -type f -name "*.json" -delete 2>/dev/null || true
find "$DOCKER_DIR/request-history" -mindepth 1 -type d -exec rm -rf {} + 2>/dev/null || true

echo "Restoring factory database..."
mkdir -p "$DOCKER_DIR/data"
cp -f "$SCRIPT_DIR/data/partio.db" "$DOCKER_DIR/data/partio.db"
# Remove WAL/SHM files if present
rm -f "$DOCKER_DIR/data/partio.db-wal" "$DOCKER_DIR/data/partio.db-shm"

echo "Restoring factory configuration..."
cp -f "$SCRIPT_DIR/partio.json" "$DOCKER_DIR/partio.json"

echo ""
echo "========================================="
echo "  Reset complete!"
echo "========================================="
echo ""
echo "Default credentials:"
echo "  User     : admin@partio / password"
echo "  Token    : default"
echo "  Admin key: partioadmin"
echo ""
echo "Run 'docker compose up -d' to start fresh."
echo ""
