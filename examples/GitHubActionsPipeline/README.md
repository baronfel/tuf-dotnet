# TUF GitHub Actions CI/CD Pipeline Templates

This directory contains GitHub Actions workflow templates for integrating TUF (The Update Framework) security into CI/CD pipelines, enabling secure software delivery and deployment verification.

## Templates Overview

### 1. Secure Release Pipeline (`secure-release.yml`)
- **Purpose**: Build, sign, and publish releases with TUF metadata
- **Use Case**: Publishing software packages, container images, or artifacts
- **Security Features**: Cryptographic signing, metadata generation, secure key management

### 2. TUF Repository Management (`tuf-repository-management.yml`)
- **Purpose**: Automate TUF repository updates and maintenance
- **Use Case**: Managing TUF metadata, key rotation, repository consistency
- **Security Features**: Automated metadata updates, signature verification, rollback protection

### 3. Secure Deployment Verification (`secure-deployment.yml`)
- **Purpose**: Verify deployments against TUF metadata before going live
- **Use Case**: Production deployment validation, artifact integrity checking
- **Security Features**: Pre-deployment verification, TUF-secured artifact validation

### 4. Multi-Repository Consensus (`multi-repo-consensus.yml`)
- **Purpose**: Implement TAP 4 multi-repository workflows in CI/CD
- **Use Case**: High-security environments requiring consensus across repositories
- **Security Features**: Multi-repository validation, consensus thresholds, conflict resolution

## Getting Started

### 1. Copy Templates to Your Repository
```bash
# Copy the templates you need to .github/workflows/
cp examples/GitHubActionsPipeline/*.yml .github/workflows/

# Customize the templates for your specific needs
# See configuration sections below
```

### 2. Configure Repository Secrets
Set up the following secrets in your GitHub repository:

```bash
# Required secrets for TUF signing
TUF_SIGNING_KEY_ROOT      # Root signing key (PEM format)
TUF_SIGNING_KEY_TARGETS   # Targets signing key (PEM format)  
TUF_SIGNING_KEY_SNAPSHOT  # Snapshot signing key (PEM format)
TUF_SIGNING_KEY_TIMESTAMP # Timestamp signing key (PEM format)

# Repository configuration
TUF_REPOSITORY_URL        # Your TUF repository URL
TUF_METADATA_BUCKET       # S3 bucket or storage for metadata
TUF_TARGETS_BUCKET        # S3 bucket or storage for targets

# Optional: Multi-repository setup
TUF_REPO_A_URL           # Primary repository URL
TUF_REPO_B_URL           # Secondary repository URL
TUF_CONSENSUS_THRESHOLD   # Required agreement threshold
```

### 3. Repository Variables
Configure these repository variables for flexibility:

```bash
# Build configuration
BUILD_CONFIGURATION=Release
DOTNET_VERSION=8.0.x

# TUF configuration  
TUF_SPEC_VERSION=1.0.0
TUF_METADATA_EXPIRY_DAYS=30
TUF_CONSISTENT_SNAPSHOT=true

# Deployment configuration
DEPLOYMENT_ENVIRONMENT=production
VERIFICATION_TIMEOUT_MINUTES=10
```

## Template Details

### Secure Release Pipeline

This workflow demonstrates end-to-end secure release management:

```yaml
name: Secure Release with TUF
on:
  push:
    tags: ['v*']
  
jobs:
  secure-release:
    runs-on: ubuntu-latest
    steps:
      # 1. Build and test
      - name: Build Application
        run: dotnet build --configuration Release
        
      - name: Run Tests  
        run: dotnet test --no-build
        
      # 2. Create TUF-signed release
      - name: Generate TUF Metadata
        uses: ./.github/actions/generate-tuf-metadata
        with:
          signing-key: ${{ secrets.TUF_SIGNING_KEY_TARGETS }}
          
      # 3. Publish to secure repository
      - name: Publish to TUF Repository
        run: |
          dotnet run --project tools/TufRepositoryManager \
            -- publish-release \
            --version ${{ github.ref_name }} \
            --metadata-url ${{ vars.TUF_REPOSITORY_URL }}
```

### TUF Repository Management

Automates repository maintenance and key rotation:

```yaml
name: TUF Repository Management
on:
  schedule:
    - cron: '0 2 * * 1'  # Weekly maintenance
  workflow_dispatch:

jobs:
  repository-maintenance:
    steps:
      # Refresh metadata with new expiry times
      - name: Update Metadata Expiry
        run: |
          dotnet run --project tools/TufRepositoryManager \
            -- refresh-metadata \
            --expiry-days ${{ vars.TUF_METADATA_EXPIRY_DAYS }}
            
      # Validate repository consistency
      - name: Verify Repository Consistency
        run: |
          dotnet run --project tools/TufRepositoryManager \
            -- verify-consistency \
            --fix-issues true
```

### Secure Deployment Verification

Validates deployments before they go live:

```yaml
name: Secure Deployment Verification
on:
  deployment:
  
jobs:
  pre-deployment-verification:
    steps:
      # Download deployment artifacts
      - name: Download Artifacts
        uses: actions/download-artifact@v3
        
      # Verify against TUF repository
      - name: TUF Verification
        run: |
          dotnet run --project examples/CliTool \
            -- verify-deployment \
            --artifacts ./artifacts/ \
            --tuf-repo ${{ vars.TUF_REPOSITORY_URL }}
            
      # Deploy only if verification passes
      - name: Deploy Application
        if: success()
        run: |
          echo "✅ TUF verification passed, proceeding with deployment"
          # Your deployment commands here
```

## Advanced Configuration

### Multi-Environment Setup

Configure different TUF repositories for different environments:

```yaml
# .github/workflows/multi-environment-deploy.yml
strategy:
  matrix:
    environment: [staging, production]
    include:
      - environment: staging
        tuf_repo: https://staging-tuf.example.com
        consensus_threshold: 1
      - environment: production  
        tuf_repo: https://prod-tuf.example.com
        consensus_threshold: 2
```

### Custom Actions

Create reusable actions for common TUF operations:

```yaml
# .github/actions/tuf-verify/action.yml
name: 'TUF Verification'
description: 'Verify artifacts against TUF repository'
inputs:
  artifacts-path:
    description: 'Path to artifacts directory'
    required: true
  tuf-repository:
    description: 'TUF repository URL'
    required: true
    
runs:
  using: 'composite'
  steps:
    - name: Run TUF Verification
      shell: bash
      run: |
        dotnet run --project ${{ github.action_path }}/../../examples/CliTool \
          -- verify ${{ inputs.artifacts-path }} \
          --metadata-url ${{ inputs.tuf-repository }}
```

### Security Best Practices

1. **Key Management**
   ```yaml
   # Use environment-specific keys
   - name: Configure TUF Keys
     env:
       TUF_KEY_ROOT: ${{ secrets[format('TUF_KEY_ROOT_{0}', github.ref_name)] }}
   ```

2. **Verification Steps**
   ```yaml
   # Always verify before deployment
   - name: Mandatory TUF Verification
     run: |
       if ! dotnet run --project tools/TufVerifier -- verify-all; then
         echo "❌ TUF verification failed - blocking deployment"
         exit 1
       fi
   ```

3. **Audit Logging**
   ```yaml
   # Log all TUF operations for audit
   - name: Log TUF Operations
     run: |
       echo "TUF_OPERATION=sign_release" >> $GITHUB_ENV
       echo "TUF_TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> $GITHUB_ENV
   ```

## Integration Examples

### Container Registry Integration
```yaml
# Secure container publishing with TUF
- name: Build and Sign Container
  run: |
    docker build -t myapp:${{ github.sha }} .
    
    # Generate TUF metadata for container
    dotnet run --project tools/TufContainerSigning \
      -- sign-container myapp:${{ github.sha }} \
      --tuf-repo ${{ vars.TUF_REPOSITORY_URL }}
```

### Kubernetes Deployment  
```yaml
# Verify Kubernetes manifests with TUF
- name: Verify K8s Manifests
  run: |
    dotnet run --project tools/TufKubernetesVerifier \
      -- verify-manifests ./k8s/ \
      --tuf-repo ${{ vars.TUF_REPOSITORY_URL }}
      
    kubectl apply -f ./k8s/
```

### Dependency Verification
```yaml  
# Verify all dependencies before build
- name: Verify Dependencies
  run: |
    dotnet run --project examples/NuGetIntegration \
      -- verify-packages packages.lock.json \
      --tuf-repo ${{ vars.TUF_PACKAGES_REPO }}
```

## Monitoring and Observability

### Pipeline Metrics
```yaml
# Track TUF verification metrics
- name: Record Metrics
  run: |
    echo "tuf_verification_duration_seconds ${{ env.VERIFICATION_TIME }}" >> metrics.txt
    echo "tuf_verification_success 1" >> metrics.txt
```

### Security Notifications
```yaml
# Alert on verification failures
- name: Security Alert
  if: failure()
  run: |
    curl -X POST ${{ secrets.SECURITY_WEBHOOK_URL }} \
      -d "TUF verification failed for ${{ github.repository }}@${{ github.sha }}"
```

## Troubleshooting

### Common Issues

1. **Metadata Expiry**
   ```bash
   # Check metadata expiry times
   dotnet run --project examples/CliTool -- info --metadata-url $TUF_REPO_URL
   
   # Refresh expired metadata
   dotnet run --project tools/TufRepositoryManager -- refresh-metadata
   ```

2. **Key Rotation**
   ```bash
   # Rotate keys safely
   dotnet run --project tools/TufKeyRotation \
     -- rotate-key targets \
     --old-key $OLD_KEY --new-key $NEW_KEY
   ```

3. **Repository Consistency**
   ```bash
   # Check for consistency issues
   dotnet run --project tools/TufRepositoryManager -- verify-consistency
   ```

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [TUF Specification](https://theupdateframework.io/specification/)  
- [Secure CI/CD Best Practices](https://www.cisa.gov/sites/default/files/publications/ESF_SECURING_THE_SOFTWARE_SUPPLY_CHAIN_DEVELOPERS.PDF)
- [SLSA Framework](https://slsa.dev/)

---

> These templates provide production-ready patterns for integrating TUF security into GitHub Actions workflows. Customize them based on your specific security requirements and deployment patterns.