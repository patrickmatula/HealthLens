# syntax=docker/dockerfile:1

FROM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/HealthLens.Api/HealthLens.Api.csproj backend/HealthLens.Api/
RUN dotnet restore backend/HealthLens.Api/HealthLens.Api.csproj
COPY backend/HealthLens.Api/ backend/HealthLens.Api/
COPY --from=frontend /src/backend/HealthLens.Api/wwwroot backend/HealthLens.Api/wwwroot
RUN dotnet publish backend/HealthLens.Api/HealthLens.Api.csproj -c Release -o /app/publish --no-restore \
    && mkdir -p /app/publish/App_Data

# Ubuntu-based "chiseled" image: no shell, no package manager, non-root by default — the smallest
# image that still ships full ICU and glibc, which Microsoft.Data.Sqlite's native library needs
# (unlike Alpine's musl, which historically needs extra packages for the same library).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=backend --chown=app:app /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
VOLUME /app/App_Data
ENTRYPOINT ["dotnet", "HealthLens.Api.dll"]
