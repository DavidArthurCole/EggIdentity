# syntax=docker/dockerfile:1.7-labs
# Build context is the repo root (ProjectReferences reach outside this project's own directory).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SyncKit.slnx Directory.Build.props global.json ./
COPY --parents src/*/*.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/SyncKit.Identity.Host/SyncKit.Identity.Host.csproj

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/SyncKit.Identity.Host/SyncKit.Identity.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8090
ENTRYPOINT ["dotnet", "SyncKit.Identity.Host.dll"]
