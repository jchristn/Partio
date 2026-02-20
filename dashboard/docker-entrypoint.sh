#!/bin/sh

# Generate runtime environment config from environment variables
cat <<EOF > /usr/share/nginx/html/env-config.js
window.__PARTIO_ENV__ = {
  PARTIO_SERVER_URL: "${PARTIO_SERVER_URL:-http://localhost:8400}"
};
EOF

# Start nginx
exec nginx -g 'daemon off;'
