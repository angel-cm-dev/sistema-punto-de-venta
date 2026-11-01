# syntax=docker/dockerfile:1

# ---------- Etapa 1: build de la SPA ----------
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# ---------- Etapa 2: nginx sin privilegios sirviendo estáticos ----------
# nginx-unprivileged corre como usuario "nginx" (UID 101) y escucha en 8080.
FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
