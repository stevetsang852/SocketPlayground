FROM node:20-alpine
WORKDIR /app
COPY SocketIoNodejs/package.json SocketIoNodejs/package-lock.json* ./
RUN npm ci --omit=dev || npm install --omit=dev
COPY SocketIoNodejs/ .
CMD ["node", "canonical-client.js", "--help"]
