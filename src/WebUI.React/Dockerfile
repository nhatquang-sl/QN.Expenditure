ARG NODE_VERSION=20.18.2

FROM node:${NODE_VERSION}-alpine

# Use production node environment by default.
ENV NODE_ENV=production

WORKDIR /app

COPY src/WebUI.React .
# RUN corepack enable

# Download dependencies as a separate step to take advantage of Docker's caching.
# Leverage a cache mount to /root/.npm to speed up subsequent builds.
# Leverage a bind mounts to package.json and package-lock.json to avoid having to copy them into
# into this layer.
RUN npm i -g serve
RUN npm i
    
# RUN cd node_modules && ls
RUN npm run build
RUN rm -rf node_modules

# Copy the rest of the source files into the image.
# COPY . .

# Expose the port that the application listens on.
EXPOSE 5174

# Run the application.
CMD [ "serve", "-s", "dist", "-l", "5174" ]
# docker build . --no-cache --progress plain -t qex.ui:1.0 -f src/WebUI.React/Dockerfile
# docker build . -t qex.ui:1.0 -f src/WebUI.React/Dockerfile
# docker run -it --rm -p 8000:8080 qex.ui:1.0
# http://localhost:8000/api/weatherforecast

# docker tag qex.ui:1.0 nhatquang/qex.ui:1.0
# docker push nhatquang/qex.ui:1.0
# docker pull nhatquang/qex.ui:1.0
# docker run -d -p 5174:5174 nhatquang/qex.ui:1.0