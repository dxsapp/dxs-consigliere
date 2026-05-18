FROM node:22-alpine AS admin-ui-build

WORKDIR /build/admin-ui

COPY ./src/admin-ui/package.json ./src/admin-ui/pnpm-lock.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile

COPY ./src/admin-ui ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

WORKDIR /build
ARG BUILD_CONFIGURATION=Release

# Copy everything
COPY ./src ./
COPY --from=admin-ui-build /build/Dxs.Consigliere/wwwroot ./Dxs.Consigliere/wwwroot
# Restore as distinct layers
RUN dotnet restore ./Dxs.Consigliere/Dxs.Consigliere.csproj
# Build and publish a release
RUN dotnet publish ./Dxs.Consigliere/Dxs.Consigliere.csproj -c ${BUILD_CONFIGURATION} -o out -p:UseAppHost=false -p:SkipAdminUiBuild=true

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /build

COPY --from=build-env /build/out .
ENV ASPNETCORE_HTTP_PORTS=5000

ENTRYPOINT ["dotnet", "Dxs.Consigliere.dll"]
EXPOSE 5000
