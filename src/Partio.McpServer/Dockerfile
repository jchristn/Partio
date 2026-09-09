FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY sdk/csharp/Partio.Sdk/Partio.Sdk.csproj sdk/csharp/Partio.Sdk/
COPY src/Partio.McpServer/Partio.McpServer.csproj src/Partio.McpServer/
RUN dotnet restore src/Partio.McpServer/Partio.McpServer.csproj -a $TARGETARCH
COPY sdk/csharp/Partio.Sdk/ sdk/csharp/Partio.Sdk/
COPY src/Partio.McpServer/ src/Partio.McpServer/
RUN dotnet publish src/Partio.McpServer/Partio.McpServer.csproj -c Release -f net10.0 -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    iputils-ping net-tools traceroute wget curl vim dnsutils netcat-openbsd iproute2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /app/logs
COPY --from=build /app/publish .
EXPOSE 8500
ENTRYPOINT ["dotnet", "partio-mcp.dll"]
