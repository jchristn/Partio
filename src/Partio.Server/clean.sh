#!/bin/bash
echo "Cleaning Partio data files..."
rm -f partio.json
rm -f partio.db
rm -rf logs
rm -rf request-history
echo "Done."
