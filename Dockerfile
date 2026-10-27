# syntax=docker/dockerfile:1
# ─── The production Dockerfile: multi-stage, layer-cached, non-root. ───
#
# Stage 1 (build) uses the full SDK image: it compiles and publishes.
# Stage 2 (final) uses the slim ASP.NET runtime image: it only runs.
# The SDK never ships, which is most of the size saving.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Layer caching, step 1: copy ONLY the project file, then restore.
# Docker caches this layer. It is rebuilt only when the .csproj changes —
# i.e. when dependencies change. Editing a .cs file does NOT redo restore.
COPY src/DockerDemo.Api/DockerDemo.Api.csproj src/DockerDemo.Api/
RUN dotnet restore src/DockerDemo.Api/DockerDemo.Api.csproj

# Layer caching, step 2: now copy the rest of the source and publish.
# A code change rebuilds only this layer and the ones after it.
COPY . .
RUN dotnet publish src/DockerDemo.Api/DockerDemo.Api.csproj \
    -c Release -o /app/publish --no-restore

# ─── Final image: runtime only, no SDK, no source, no build tools. ───
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Non-root: .NET 8 runtime images ship an unprivileged `app` user.
# A container breakout as `app` is far less useful than one as root,
# and many clusters refuse to run root containers at all.
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DockerDemo.Api.dll"]
