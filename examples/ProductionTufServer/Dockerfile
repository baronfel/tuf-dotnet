# Production TUF Repository Server
# Multi-stage build for optimal image size and security

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy project files first for better caching
COPY ["examples/ProductionTufServer/ProductionTufServer.csproj", "examples/ProductionTufServer/"]
COPY ["TUF/TUF.csproj", "TUF/"]
COPY ["CanonicalJson/CanonicalJson.csproj", "CanonicalJson/"]
COPY ["Directory.Packages.props", ""]
COPY ["nuget.config", ""]

# Restore dependencies
RUN dotnet restore "examples/ProductionTufServer/ProductionTufServer.csproj"

# Copy source code
COPY . .

# Build and publish the application
WORKDIR /src/examples/ProductionTufServer
RUN dotnet publish -c Release -o /app/publish --no-restore --self-contained false

# Runtime stage - use minimal runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Create non-root user for security
RUN addgroup -g 1001 -S tufserver && \
    adduser -u 1001 -S tufserver -G tufserver

# Install security updates
RUN apk upgrade --no-cache

# Create application directory and set permissions
WORKDIR /app
RUN chown tufserver:tufserver /app

# Copy published application
COPY --from=build --chown=tufserver:tufserver /app/publish .

# Create data directories with proper permissions
RUN mkdir -p /app/metadata /app/targets && \
    chown -R tufserver:tufserver /app/metadata /app/targets

# Switch to non-root user
USER tufserver

# Configure environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV TUF_METADATA_PATH=/app/metadata
ENV TUF_TARGETS_PATH=/app/targets

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Security labels
LABEL org.opencontainers.image.title="Production TUF Repository Server" \
      org.opencontainers.image.description="Production-ready TUF metadata and targets server built with .NET" \
      org.opencontainers.image.vendor="TUF .NET Project" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/baronfel/tuf-dotnet"

# Run the application
ENTRYPOINT ["dotnet", "ProductionTufServer.dll"]