# ── Build stage ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all source from repo root (context must be repo root)
COPY . .

# Restore & publish API
WORKDIR /src/src/PersianAiChat.Api
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Create non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

WORKDIR /app
COPY --from=build /app/publish ./

# Data directory for SQLite
RUN mkdir -p /data && chown appuser:appgroup /data

# Run as non-root
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Default="Data Source=/data/persianaichat.db"

EXPOSE 8080

ENTRYPOINT ["dotnet", "PersianAiChat.Api.dll"]
