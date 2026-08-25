# Build context is this directory (frontend/FinancialStatementAI.Web/), e.g.:
#   docker build -t financialstatementai-web .
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist/FinancialStatementAI.Web/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
