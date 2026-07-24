FROM nginx:alpine

# Install Node.js for WebSocket server
RUN apk add --no-cache nodejs npm

# Copy WebSocket server (from project root Server/)
WORKDIR /app/server
COPY Server/ .
RUN npm ci --omit=dev

# Copy nginx configuration
COPY Deploy/nginx/nginx.conf /etc/nginx/nginx.conf

# Copy WebGL build output
COPY Deploy/webgl-build/ /usr/share/nginx/html/

# Verify build artifacts are present (fail fast if build is incomplete)
RUN test -f /usr/share/nginx/html/index.html \
    && ls /usr/share/nginx/html/Build/*.loader.js >/dev/null 2>&1 \
    && ls /usr/share/nginx/html/Build/*.framework.js* >/dev/null 2>&1 \
    && ls /usr/share/nginx/html/Build/*.wasm* >/dev/null 2>&1 \
    && ls /usr/share/nginx/html/Build/*.data* >/dev/null 2>&1 \
    || (echo "ERROR: WebGL build artifacts incomplete — run build-webgl.sh first" && exit 1)

# Copy entrypoint
COPY Deploy/start.sh /start.sh
RUN chmod +x /start.sh

EXPOSE 80

CMD ["/start.sh"]
