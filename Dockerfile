# ============================================
# RallyAPI Dockerfile - Multi-stage build
# ============================================
# Stage 1: Build
# Stage 2: Runtime (slim)
# ============================================

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy everything (solution + all projects)
# We copy everything because the modular monolith has many project references
COPY . .

# Restore dependencies
RUN dotnet restore src/RallyAPI.Host/RallyAPI.Host.csproj

# Publish in Release mode
# RUN dotnet publish src/RallyAPI.Host/RallyAPI.Host.csproj \
#     -c Release \
#     -o /app/publish \
#     --no-restore

RUN dotnet publish src/RallyAPI.Host/RallyAPI.Host.csproj \
    -c Debug \
    -o /app/publish \
    --no-restore

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r rallyapi && useradd -r -g rallyapi -s /bin/false rallyapi

# Copy published app
COPY --from=build /app/publish .

# Create Keys directory (RSA keys will be mounted or generated at runtime)
RUN mkdir -p Keys && chown -R rallyapi:rallyapi /app

# Switch to non-root user
USER rallyapi

# Expose port
EXPOSE 8080

# .NET 8 defaults to port 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "RallyAPI.Host.dll"]
