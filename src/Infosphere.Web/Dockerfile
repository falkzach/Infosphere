FROM node:22-alpine AS build
WORKDIR /app

COPY src/Infosphere.Web/package.json ./
COPY src/Infosphere.Web/package-lock.json ./
RUN npm ci

COPY src/Infosphere.Web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY src/Infosphere.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/Infosphere.Web/config.template.js /opt/config.template.js
COPY src/Infosphere.Web/docker-entrypoint.sh /docker-entrypoint.d/30-config.sh
COPY --from=build /app/dist ./

RUN chmod +x /docker-entrypoint.d/30-config.sh

EXPOSE 80
