FROM mcr.microsoft.com/dotnet/sdk:10.0

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        bash ca-certificates curl git gnupg jq less openssh-client \
        postgresql-client gdal-bin osm2pgsql python3 python3-venv \
        vim wget zip unzip build-essential \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /bin/zsh vscode \
    && mkdir -p /home/vscode/.nuget/packages /home/vscode/.dotnet \
    && chown -R vscode:vscode /home/vscode

WORKDIR /workspace
