ARG NODE_VERSION=20.18.2

# Stage 1: build
FROM node:${NODE_VERSION}-alpine AS build

WORKDIR /app

# Copy manifests first so npm ci layer is cached independently of source changes
COPY src/WebUI.React/package.json src/WebUI.React/package-lock.json ./

RUN npm ci --include=dev

# Copy source after install to preserve cache when only source changes
COPY src/WebUI.React .

ARG VITE_API_ENDPOINT=https://quangnn.store
ENV VITE_API_ENDPOINT=$VITE_API_ENDPOINT

RUN npm run build

# Stage 2: serve static files with nginx
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

RUN printf 'server {\n    listen 80;\n    return 301 https://$host:5175$request_uri;\n}\n\nserver {\n    listen 443 ssl;\n    ssl_certificate     /etc/nginx/certs/localhost.pem;\n    ssl_certificate_key /etc/nginx/certs/localhost-key.pem;\n\n    root /usr/share/nginx/html;\n    index index.html;\n\n    location / {\n        try_files $uri $uri/ /index.html;\n    }\n}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80
EXPOSE 443

CMD ["nginx", "-g", "daemon off;"]

# docker build . --no-cache --progress plain -t qex.ui:1.0 -f src/WebUI.React/Dockerfile
# docker build . -t qex.ui:1.0 -f src/WebUI.React/Dockerfile
# docker tag qex.ui:1.0 nhatquang/qex.ui:1.0
# docker push nhatquang/qex.ui:1.0
# docker pull nhatquang/qex.ui:1.0
# docker run -d -p 80:80 nhatquang/qex.ui:1.0