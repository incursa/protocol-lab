FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj \
    --configuration Release \
    --output /app/publish

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

ENV PROTOCOL_LAB_IMPLEMENTATION=incursa-http3
ENV PROTOCOL_LAB_INCURSA_MODE=endpoint
ENV PROTOCOL_LAB_H3_PORT=5444

EXPOSE 5444/udp

ENTRYPOINT ["dotnet", "Incursa.ProtocolLab.Adapters.IncursaHttp3.dll"]
CMD ["--mode", "endpoint", "--port", "5444"]
