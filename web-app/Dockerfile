FROM node:20-alpine

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci --omit=dev

COPY src/ ./src/

# Data directory for SQLite (mount as volume for persistence)
RUN mkdir -p /app/data

ENV API_PORT=3001
ENV DB_PATH=/app/data/edi-survey.db

EXPOSE 3001

CMD ["node", "src/index.js"]
