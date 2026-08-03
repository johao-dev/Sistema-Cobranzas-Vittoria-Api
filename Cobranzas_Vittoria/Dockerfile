FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy

ENV ASPNETCORE_URLS=http://0.0.0.0:5000 \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    DOTNET_CLI_HOME=/tmp/dotnet \
    NUGET_PACKAGES=/tmp/nuget/packages

RUN apt-get update \
    && apt-get install -y --no-install-recommends git libgdiplus procps \
    && mkdir -p /tmp/dotnet /tmp/nuget/packages /workspace \
    && chmod -R 777 /tmp/dotnet /tmp/nuget /workspace \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /workspace/Cobranzas_Vittoria
