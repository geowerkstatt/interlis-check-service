# syntax=docker.io/docker/dockerfile:1
# check=error=true

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG VERSION
ARG REVISION

# Set default shell
SHELL ["/bin/bash", "-c"]

# Install Node.js
RUN apt-get update && \
    apt-get install -y gnupg && \
    mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg && \
    NODE_MAJOR=20 && \
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_$NODE_MAJOR.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list && \
    apt-get update && \
    apt-get install nodejs -y && \
    rm -rf /var/lib/apt/lists/*

# Register GeoWerkstatt nuget package registry as a NuGet source
RUN dotnet nuget add source "https://nuget.pkg.github.com/GeoWerkstatt/index.json" \
    --name "github" \
    --username "%GITHUB_NUGET_USER%" \
    --password "%GITHUB_NUGET_TOKEN%" \
    --store-password-in-clear-text

# Restore dependencies and tools
COPY ["src/Ilicop.Web/Ilicop.Web.csproj", "Ilicop.Web/"]
RUN --mount=type=secret,id=github_nuget_user,env=GITHUB_NUGET_USER \
    --mount=type=secret,id=github_nuget_token,env=GITHUB_NUGET_TOKEN \
    dotnet restore "Ilicop.Web/Ilicop.Web.csproj"

# Create optimized production build
COPY ["src/Ilicop.Web/", "Ilicop.Web/"]
ENV GENERATE_SOURCEMAP=false
ENV PUBLISH_DIR=/app/publish
RUN dotnet publish "Ilicop.Web/Ilicop.Web.csproj" \
  -c Release \
  -p:VersionPrefix=${VERSION} \
  -p:SourceRevisionId=${REVISION} \
  -o ${PUBLISH_DIR}

# Generate license and copyright notice for Node.js packages
WORKDIR /src/Ilicop.Web/ClientApp
COPY ["licenseCustomFormat.json", "/src/Ilicop.Web/ClientApp/"]
RUN npx license-checker --json --production \
  --customPath licenseCustomFormat.json \
  --out ${PUBLISH_DIR}/ClientApp/build/license.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG VERSION
ARG REVISION
ENV HOME=/app
ENV TZ=Europe/Zurich
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ILICOP_APP_VERSION=${VERSION}
ENV ILICOP_APP_REVISION=${REVISION}
ENV ILICOP_APP_HOME_DIR=/app
ENV ILICOP_APP_LOG_DIR=/logs
ENV ILICOP_UPLOADS_DIR=/uploads
ENV ILICOP_WEB_ASSETS_DIR=/web-assets
ENV ILITOOLS_HOME_DIR=/ilitools
ENV ILITOOLS_PLUGINS_DIR=/plugins
ENV ILITOOLS_CACHE_DIR=/cache
ENV ILITOOLS_MODEL_REPOSITORY_DIR=/repository
WORKDIR ${ILICOP_APP_HOME_DIR}

# Install missing packages
RUN \
  DEBIAN_FRONTEND=noninteractive && \
  mkdir -p /usr/share/man/man1 /usr/share/man/man2 && \
  apt-get update && \
  apt-get install -y curl unzip default-jre-headless sudo vim htop cron && \
  rm -rf /var/lib/apt/lists/*

# Create our folders
RUN \
 mkdir -p \
   $ILICOP_APP_HOME_DIR \
   $ILICOP_APP_LOG_DIR \
   $ILICOP_UPLOADS_DIR \
   $ILICOP_WEB_ASSETS_DIR \
   $ILITOOLS_HOME_DIR \
   $ILITOOLS_PLUGINS_DIR \
   $ILITOOLS_CACHE_DIR \
   $ILITOOLS_MODEL_REPOSITORY_DIR

# Copy default model repository files
COPY data/repositories/default $ILITOOLS_MODEL_REPOSITORY_DIR

EXPOSE 8080
VOLUME $ILICOP_APP_LOG_DIR
VOLUME $ILICOP_UPLOADS_DIR
VOLUME $ILITOOLS_PLUGINS_DIR
VOLUME $ILITOOLS_MODEL_REPOSITORY_DIR

# Set default locale
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

COPY --from=build /app/publish $ILICOP_APP_HOME_DIR
COPY docker-entrypoint.sh /entrypoint.sh

HEALTHCHECK CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["/entrypoint.sh"]
