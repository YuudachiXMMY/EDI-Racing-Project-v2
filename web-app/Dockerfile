# Stage 1: Build client (React/Vite — no native deps)
FROM node:20-alpine AS client-build
WORKDIR /app/client
COPY client/package.json client/package-lock.json* ./
RUN npm ci
COPY client/ ./
RUN npm run build

# Stage 2: Build server deps.
# better-sqlite3 ships no musl (alpine) prebuilt binary, so npm falls back to
# node-gyp and compiles from source — which needs python3 + a C/C++ toolchain.
# Keep that toolchain in THIS throwaway stage only; the final image stays slim
# (and keeps alpine's built-in wget for the compose healthcheck).
FROM node:20-alpine AS server-deps
RUN apk add --no-cache python3 make g++
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci --omit=dev

# Stage 3: Production runtime (same alpine/musl base → compiled .node is ABI-compatible)
FROM node:20-alpine
WORKDIR /app
COPY package.json ./
COPY --from=server-deps /app/node_modules ./node_modules
COPY src/ ./src/
COPY --from=client-build /app/client/dist ./client/dist
RUN mkdir -p /app/data

ENV API_PORT=3001
ENV DB_PATH=/app/data/edi-survey.db

EXPOSE 3001
CMD ["node", "src/index.js"]
