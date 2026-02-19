#!/bin/bash
# Partio Python SDK Test Harness Runner
# Usage: ./go.sh <endpoint> <access_key>
# Example: ./go.sh http://localhost:8000 partioadmin

if [ -z "$1" ] || [ -z "$2" ]; then
    echo "Usage: ./go.sh <endpoint> <access_key>"
    echo "Example: ./go.sh http://localhost:8000 partioadmin"
    exit 1
fi

python test_harness.py "$1" "$2"
