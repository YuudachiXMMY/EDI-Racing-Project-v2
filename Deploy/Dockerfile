FROM nginx:alpine

# Install Node.js for WebSocket server
RUN apk add --no-cache nodejs npm

# Copy WebSocket server (from project root Server/)
WORKDIR /app/server
COPY Server/ .
RUN npm ci --omit=dev

# Copy nginx configuration
COPY Deploy/nginx/nginx.conf /etc/nginx/nginx.conf

# Copy WebGL build output under /game/ (nginx serves the gated build from this subdir;
# the survey app owns the site root). Unity's index.html uses relative Build/ paths, so no
# rebuild is needed to serve it from a subdirectory.
COPY Deploy/webgl-build/ /usr/share/nginx/html/game/

# Host build files may be copied without world-read (or dirs without world-exec),
# which makes nginx's unprivileged worker return 403 for .br/.wasm/.data assets
# and the game hangs on the loading screen. Grant read to files, traverse to dirs.
RUN chmod -R a+rX /usr/share/nginx/html

# Verify build artifacts are present (fail fast if build is incomplete)
RUN test -f /usr/share/nginx/html/game/index.html \
    && ls /usr/share/nginx/html/game/Build/*.loader.js >/dev/null 2>&1 \
    && ls /usr/share/nginx/html/game/Build/*.framework.js* >/dev/null 2>&1 \
    && ls /usr/share/nginx/html/game/Build/*.wasm* >/dev/null 2>&1 \
    && ls /usr/share/nginx/html/game/Build/*.data* >/dev/null 2>&1 \
    || (echo "ERROR: WebGL build artifacts incomplete — run build-webgl.sh first" && exit 1)

# Copy entrypoint
COPY Deploy/start.sh /start.sh
RUN chmod +x /start.sh

# WebSocket server port — MUST match nginx.conf upstream (proxy_pass 127.0.0.1:3000).
# server.js defaults to 8080; pin 3000 here so nginx and the WS server never drift,
# even when this image is run without compose env overrides.
ENV PORT=3000

EXPOSE 80

CMD ["/start.sh"]
