FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY servers/KestrelBenchServer/KestrelBenchServer.csproj servers/KestrelBenchServer/
RUN dotnet restore servers/KestrelBenchServer/KestrelBenchServer.csproj

COPY servers/KestrelBenchServer/ servers/KestrelBenchServer/
RUN dotnet publish servers/KestrelBenchServer/KestrelBenchServer.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates wget \
    && wget -O /tmp/packages-microsoft-prod.deb https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends libmsquic \
    && rm -rf /var/lib/apt/lists/* /tmp/packages-microsoft-prod.deb

COPY --from=build /app/publish .

ENV PROTOCOL_LAB_ENABLE_EXPLICIT_ENDPOINTS=true
ENV PROTOCOL_LAB_H1_URL=http://0.0.0.0:5080
ENV PROTOCOL_LAB_H3_URL=https://0.0.0.0:5443
ENV PROTOCOL_LAB_GENERATE_LOCAL_CERT=true
ENV PROTOCOL_LAB_IMPLEMENTATION=kestrel-http3

EXPOSE 5080/tcp
EXPOSE 5443/tcp
EXPOSE 5443/udp

ENTRYPOINT ["dotnet", "KestrelBenchServer.dll"]
