FROM node:22-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_GATEWAY_URL
ARG VITE_IDENTITY_URL
ARG VITE_APP_URL
ENV VITE_GATEWAY_URL=$VITE_GATEWAY_URL
ENV VITE_IDENTITY_URL=$VITE_IDENTITY_URL
ENV VITE_APP_URL=$VITE_APP_URL
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf