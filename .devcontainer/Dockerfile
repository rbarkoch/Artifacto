FROM mcr.microsoft.com/devcontainers/dotnet:9.0-bookworm

RUN apt-get update && \
    \
    # Install Redoc CLI
    apt-get install nodejs npm -y --no-install-recommends && \
    npm i -g @redocly/cli@latest && \
    dotnet tool install -g NSwag.ConsoleCore && \
    dotnet tool install -g dotnet-ef && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ENV PATH="${PATH}:/root/.dotnet/tools"

CMD [ "/bin/pwsh" ]