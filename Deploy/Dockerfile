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

# Copy entrypoint
COPY Deploy/start.sh /start.sh
RUN chmod +x /start.sh

EXPOSE 80

CMD ["/start.sh"]
