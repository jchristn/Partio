FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Partio.Core/Partio.Core.csproj Partio.Core/
COPY Partio.Server/Partio.Server.csproj Partio.Server/
RUN dotnet restore Partio.Server/Partio.Server.csproj -a $TARGETARCH
COPY Partio.Core/ Partio.Core/
COPY Partio.Server/ Partio.Server/
RUN dotnet publish Partio.Server/Partio.Server.csproj -c Release -f net10.0 -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    iputils-ping net-tools traceroute wget curl vim dnsutils netcat-openbsd iproute2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /app/logs /app/data /app/request-history
COPY --from=build /app/publish .
EXPOSE 8400
ENTRYPOINT ["dotnet", "Partio.Server.dll"]
