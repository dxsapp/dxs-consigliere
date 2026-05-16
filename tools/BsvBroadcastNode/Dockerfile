FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/Dxs.Bsv/                    src/Dxs.Bsv/
COPY src/Dxs.Common/                  src/Dxs.Common/
COPY tools/BsvBroadcastNode/          tools/BsvBroadcastNode/

WORKDIR /src/tools/BsvBroadcastNode
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Peer pool size
ENV BSV_BroadcastNode__PoolSize=8
# User-agent (some operators require /Bitcoin SV:X.Y.Z/)
ENV BSV_BroadcastNode__UserAgent="/Bitcoin SV:1.2.1/"
# Extra comma-separated seed peers: "1.2.3.4:8333,5.6.7.8:8333"
ENV BSV_BroadcastNode__ExtraPeers=""

EXPOSE 8080

ENTRYPOINT ["dotnet", "BsvBroadcastNode.dll"]
