#!/bin/sh
# Start WebSocket server in background
cd /app/server && node server.js &

# Start nginx in foreground (PID 1 keeps container alive)
nginx -g 'daemon off;'
