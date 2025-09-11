# TUF .NET Docker Integration Example
# Demonstrates secure software update capabilities in containerized environments

# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Create directories for TUF operations
RUN mkdir -p /app/trusted /app/downloads /app/metadata && \
    chmod 755 /app/trusted /app/downloads /app/metadata

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["examples/DockerIntegration/DockerIntegration.csproj", "examples/DockerIntegration/"]
COPY ["TUF/TUF.csproj", "TUF/"]
COPY ["CanonicalJson/CanonicalJson.csproj", "CanonicalJson/"]
COPY ["Directory.Packages.props", "./"]
COPY ["TUF.slnx", "./"]
RUN dotnet restore "examples/DockerIntegration/DockerIntegration.csproj"

# Copy source code and build the application
COPY . .
WORKDIR "/src/examples/DockerIntegration"
RUN dotnet build "DockerIntegration.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "DockerIntegration.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables for container operation
ENV TUF_MetadataUrl=""
ENV TUF_RootKeyPath="/app/trusted/root.json"
ENV TUF_DownloadsDir="/app/downloads"
ENV TUF_MetadataDir="/app/metadata"
ENV TUF_MaxMetadataBytes="1048576"
ENV TUF_MaxTargetBytes="104857600"

# Create non-root user for security
RUN groupadd -r tufuser && useradd -r -g tufuser tufuser
RUN chown -R tufuser:tufuser /app
USER tufuser

# Add health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD dotnet DockerIntegration.dll health || exit 1

# Default command
ENTRYPOINT ["dotnet", "DockerIntegration.dll"]