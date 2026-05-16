FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution structure needed for the project reference
COPY src/Dxs.Bsv/                          src/Dxs.Bsv/
COPY src/Dxs.Common/                       src/Dxs.Common/
COPY tests/Spikes/P2p/Spike.Soak/          tests/Spikes/P2p/Spike.Soak/

WORKDIR /src/tests/Spikes/P2p/Spike.Soak
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish .

ENV POOL_SIZE=8
ENV SOAK_MINUTES=1440
ENV STATS_INTERVAL_SEC=60
ENV USER_AGENT=/ConsigliereThinNode:0.1.0/
ENV EXTRA_PEERS=

ENTRYPOINT ["dotnet", "Spike.Soak.dll"]
