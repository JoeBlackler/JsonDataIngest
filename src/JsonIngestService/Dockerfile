# ---- build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["JsonIngestService.csproj", "."]
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---- runtime stage ------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Non-root user for security hardening
RUN addgroup --system --gid 1001 ingest && \
    adduser  --system --uid 1001 --ingroup ingest ingest

# Volume mounts for file drop, processed, and error paths
VOLUME ["/data/ingest/drop", "/data/ingest/processed", "/data/ingest/error"]

COPY --from=build /app/publish .
RUN chown -R ingest:ingest /app /data || true

USER ingest

ENTRYPOINT ["dotnet", "JsonIngestService.dll"]
