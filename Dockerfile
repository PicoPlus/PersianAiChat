# ── Build stage ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution & project files first (layer cache optimization)
COPY PersianAiChat.sln ./
COPY src/PersianAiChat.Domain/PersianAiChat.Domain.csproj                 src/PersianAiChat.Domain/
COPY src/PersianAiChat.Application/PersianAiChat.Application.csproj       src/PersianAiChat.Application/
COPY src/PersianAiChat.Infrastructure/PersianAiChat.Infrastructure.csproj src/PersianAiChat.Infrastructure/
COPY src/PersianAiChat.Api/PersianAiChat.Api.csproj                       src/PersianAiChat.Api/
COPY tests/PersianAiChat.UnitTests/PersianAiChat.UnitTests.csproj         tests/PersianAiChat.UnitTests/
COPY tests/PersianAiChat.IntegrationTests/PersianAiChat.IntegrationTests.csproj tests/PersianAiChat.IntegrationTests/

# Restore
RUN dotnet restore

# Copy all source
COPY . .

# Build & publish API
WORKDIR /src/src/PersianAiChat.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

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
