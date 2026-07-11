# syntax=docker/dockerfile:1

FROM node:24-alpine AS client-build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund

COPY scripts ./scripts
RUN npm run build:client

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Host/Host.csproj Host/packages.lock.json ./Host/
RUN dotnet restore Host/Host.csproj --locked-mode

COPY Host ./Host
COPY --from=client-build /src/Host/wwwroot/Scripts/vendor ./Host/wwwroot/Scripts/vendor
RUN dotnet publish Host/Host.csproj \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    --output /app/publish \
    -p:SkipClientBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    RENZYU_GAME_TELEMETRY_DIRECTORY=/data/telemetry \
    RENZYU_AI_MODEL_DIRECTORY=/data/models
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /data/telemetry /data/models \
    && chown $APP_UID:$APP_UID /data/telemetry /data/models
VOLUME ["/data/telemetry", "/data/models"]
USER $APP_UID

ENTRYPOINT ["dotnet", "Host.dll"]
