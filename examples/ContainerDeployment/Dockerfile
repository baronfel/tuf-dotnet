# TUF .NET Container Deployment Example
# Multi-stage Docker build optimized for security and performance

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# Copy project files and restore dependencies
COPY examples/ContainerDeployment/ContainerDeployment.csproj ./examples/ContainerDeployment/
COPY TUF/TUF.csproj ./TUF/
COPY CanonicalJson/CanonicalJson.csproj ./CanonicalJson/
COPY Directory.Packages.props ./

RUN dotnet restore examples/ContainerDeployment/ContainerDeployment.csproj

# Copy source code and build
COPY examples/ContainerDeployment/ ./examples/ContainerDeployment/
COPY TUF/ ./TUF/
COPY CanonicalJson/ ./CanonicalJson/

RUN dotnet publish examples/ContainerDeployment/ContainerDeployment.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Security: Create non-root user
RUN addgroup -g 1001 -S tuf && \
    adduser -S -u 1001 -G tuf tuf

# Set working directory
WORKDIR /app

# Copy application from build stage
COPY --from=build /app .

# Create directories for TUF metadata and targets
RUN mkdir -p /tmp/tuf-metadata /tmp/tuf-targets && \
    chown -R tuf:tuf /tmp/tuf-metadata /tmp/tuf-targets /app

# Switch to non-root user
USER tuf

# Environment variables for container deployment
ENV ASPNETCORE_URLS=http://+:8080
ENV TUF_METADATA_URL=https://example.com/metadata
ENV TUF_TARGETS_URL=https://example.com/targets
ENV TUF_TRUSTED_ROOT_PATH=/app/trusted-root.json

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "ContainerDeployment.dll"]