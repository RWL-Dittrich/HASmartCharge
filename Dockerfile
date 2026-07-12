# syntax=docker/dockerfile:1
# Multi-stage build for the HASmartCharge Home Assistant add-on image.
# Produces a single container where the ASP.NET backend serves the API, the OCPP
# WebSocket endpoint, and the built React SPA (from wwwroot).

# 1) Build the React SPA -> /fe/dist
FROM node:22-bookworm-slim AS frontend
WORKDIR /fe
COPY HASmartCharge.Frontend/package.json HASmartCharge.Frontend/package-lock.json ./
RUN npm ci
COPY HASmartCharge.Frontend/ ./
RUN npm run build

# 2) Publish the ASP.NET backend -> /app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY . .
RUN dotnet restore HASmartCharge.Backend/HASmartCharge.Backend.csproj
RUN dotnet publish HASmartCharge.Backend/HASmartCharge.Backend.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

# 3) Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /fe/dist ./wwwroot

# /data is the add-on's persistent volume (HA mounts it); ensure it exists for standalone runs.
RUN mkdir -p /data

# 8099: HTTP for the UI + API (fronted by HA ingress). 8180: OCPP 1.6J WebSocket (charger connects directly).
ENV ASPNETCORE_URLS="http://+:8099;http://+:8180" \
    ConnectionStrings__DefaultConnection="Data Source=/data/hasmartcharge.db"
EXPOSE 8099 8180

ENTRYPOINT ["dotnet", "HASmartCharge.Backend.dll"]
