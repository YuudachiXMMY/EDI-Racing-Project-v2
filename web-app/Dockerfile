# Stage 1: Build client
FROM node:20-alpine AS client-build
WORKDIR /app/client
COPY client/package.json client/package-lock.json* ./
RUN npm ci
COPY client/ ./
RUN npm run build

# Stage 2: Production
FROM node:20-alpine
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci --omit=dev
COPY src/ ./src/
COPY --from=client-build /app/client/dist ./client/dist
RUN mkdir -p /app/data

ENV API_PORT=3001
ENV DB_PATH=/app/data/edi-survey.db

EXPOSE 3001
CMD ["node", "src/index.js"]
