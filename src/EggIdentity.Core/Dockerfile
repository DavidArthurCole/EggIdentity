# syntax=docker/dockerfile:1.7-labs
# Build context is the repo root (ProjectReferences reach outside this project's own directory).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/EggIdentity.Core/EggIdentity.Core.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8095
ENTRYPOINT ["dotnet", "EggIdentity.Core.dll"]
