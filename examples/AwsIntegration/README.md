# TUF .NET AWS Integration

This example demonstrates comprehensive AWS cloud integration for TUF (The Update Framework) .NET implementation, providing enterprise-grade security and scalability features that achieve parity with the AWS capabilities found in other TUF implementations like Rust's `tough-ssm` and `tough-kms`.

## 🎯 Parity Achievement

This implementation addresses **Phase 3: Cloud Integration** from the TUF .NET parity analysis by providing:

- **AWS KMS Integration**: Hardware Security Module (HSM) backed signing
- **Amazon S3 Repository Backend**: Scalable, durable TUF metadata and target storage  
- **AWS Systems Manager Parameter Store**: Centralized, secure configuration management
- **Production-Ready Architecture**: Complete enterprise deployment patterns

## 🏗️ Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   TUF Client    │    │   TUF .NET       │    │   AWS Cloud     │
│                 │    │   Application    │    │                 │
│ • Updater       │◄──►│ • KMS Signer     │◄──►│ • KMS HSM       │
│ • Verification  │    │ • S3 Repository  │    │ • S3 Storage    │
│ • Downloads     │    │ • Parameter Store│    │ • Parameter     │
└─────────────────┘    └──────────────────┘    │   Store Config  │
                                               └─────────────────┘
```

## 🚀 Quick Start

### Prerequisites

1. **AWS Account** with appropriate permissions for KMS, S3, and SSM
2. **AWS CLI configured** with valid credentials:
   ```bash
   aws configure
   # or use IAM roles, environment variables, etc.
   ```
3. **.NET 8.0 or later** installed

### Basic Usage

```bash
# Build the example
dotnet build

# 1. Generate a KMS signing key
dotnet run -- kms generate-key --region us-east-1

# 2. Set up Parameter Store configuration  
dotnet run -- params setup --prefix /tuf/demo --region us-east-1

# 3. Upload TUF metadata to S3
dotnet run -- s3 upload-demo --bucket your-tuf-bucket --region us-east-1

# 4. Complete integration demonstration
dotnet run -- full-demo \
  --kms-key-id "alias/tuf-dotnet-key" \
  --s3-bucket "your-tuf-bucket" \
  --param-prefix "/tuf/demo" \
  --region us-east-1
```

## 🔐 AWS KMS Integration

### Features

- **Hardware Security Module (HSM) Protection**: Keys never leave AWS infrastructure
- **Multi-Algorithm Support**: RSA-2048, RSA-3072, RSA-4096 with RSASSA-PSS-SHA256
- **Centralized Key Management**: Key rotation, versioning, and lifecycle management
- **Audit Trail**: Complete CloudTrail logging for all signing operations
- **Cross-Region Support**: Multi-region key availability

### Usage Example

```csharp
using var kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.USEast1);
using var signer = await AwsKmsSigner.CreateAsync(kmsClient, "alias/my-tuf-key");

// Sign TUF metadata
var signature = signer.SignBytes(metadataBytes);

// Use in TUF metadata
var signedMetadata = metadata with { Signatures = [signature] };
```

### Key Creation

```bash
# Generate RSA-3072 key (recommended)
dotnet run -- kms generate-key --region us-east-1

# This creates:
# - KMS key with appropriate permissions
# - Alias for easier management (alias/tuf-dotnet-YYYYMMDD-HHMMSS)
# - Proper tags for identification
```

### Security Considerations

- **IAM Permissions**: Use least-privilege policies
- **Key Policy**: Restrict access to authorized principals only
- **Multi-Factor Authentication**: Require MFA for sensitive operations
- **Key Rotation**: Enable automatic key rotation where supported

## 📦 Amazon S3 Repository Backend

### Features

- **99.999999999% Durability**: Industry-leading data protection
- **Global Distribution**: CloudFront integration for worldwide availability
- **Server-Side Encryption**: Automatic encryption at rest with KMS
- **Versioning Support**: Complete history of metadata changes
- **Access Control**: Fine-grained IAM and bucket policies

### Repository Structure

```
s3://your-tuf-bucket/
├── metadata/
│   ├── root.json           # Root metadata with public keys
│   ├── timestamp.json      # Timestamp metadata (frequently updated)
│   ├── snapshot.json       # Snapshot metadata
│   └── targets.json        # Targets metadata
└── targets/
    └── releases/
        └── v1.0/
            └── app.exe     # Target files with original paths
```

### Usage Example

```csharp
using var s3Client = new AmazonS3Client(RegionEndpoint.USEast1);
using var repository = new S3RepositoryBackend(s3Client, "my-tuf-bucket");

// Upload metadata
await repository.UploadMetadataAsync("root.json", rootMetadataJson);

// Upload target files
await repository.UploadTargetAsync("releases/v1.0/app.exe", fileStream);

// Download for clients
var metadata = await repository.DownloadMetadataAsync("timestamp.json");
var targetStream = await repository.DownloadTargetAsync("releases/v1.0/app.exe");
```

### Performance Optimization

- **CloudFront CDN**: Distribute globally with edge caching
- **Transfer Acceleration**: Faster uploads via AWS edge locations
- **Multipart Upload**: Efficient handling of large target files
- **Intelligent Tiering**: Automatic cost optimization

## ⚙️ AWS Systems Manager Parameter Store

### Features

- **Hierarchical Configuration**: Organized parameter structure
- **Encryption at Rest**: SecureString parameters encrypted with KMS
- **Access Control**: IAM-based parameter access policies
- **Change Tracking**: Complete audit trail for configuration changes
- **Environment Isolation**: Separate configurations per environment

### Parameter Structure

```
/tuf/{environment}/
├── kms/
│   ├── region                    # AWS region for KMS
│   └── signing-key-id           # KMS key ID or alias
├── s3/
│   ├── region                   # AWS region for S3
│   ├── repository-bucket        # S3 bucket name
│   ├── metadata-prefix          # Metadata path prefix
│   └── targets-prefix           # Targets path prefix
├── repository/
│   ├── name                     # Repository identifier
│   ├── root-expiry-days         # Root metadata validity period
│   ├── timestamp-expiry-hours   # Timestamp metadata validity
│   ├── snapshot-expiry-days     # Snapshot metadata validity
│   └── targets-expiry-days      # Targets metadata validity
├── security/
│   ├── signature-threshold      # Required signature count
│   └── consistent-snapshot      # Enable atomic updates
└── secrets/
    └── api-token               # Secure API tokens (SecureString)
```

### Usage Example

```csharp
using var ssmClient = new AmazonSimpleSystemsManagementClient(RegionEndpoint.USEast1);
using var config = new ParameterStoreConfig(ssmClient, "/tuf/production");

// Load complete configuration
var tufConfig = await config.GetTufConfigurationAsync();

// Access individual parameters
var apiToken = await config.GetParameterAsync("secrets/api-token", decrypt: true);

// Set configuration values
await config.SetParameterAsync("repository/version", "2.0", 
    ParameterType.String, description: "Current repository version");
```

## 🏭 Production Deployment

### Infrastructure as Code

#### AWS CDK (TypeScript)

```typescript
import * as cdk from 'aws-cdk-lib';
import * as kms from 'aws-cdk-lib/aws-kms';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as ssm from 'aws-cdk-lib/aws-ssm';

export class TufAwsStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    // KMS Key for TUF signing
    const signingKey = new kms.Key(this, 'TufSigningKey', {
      description: 'TUF .NET signing key',
      keyUsage: kms.KeyUsage.SIGN_VERIFY,
      keySpec: kms.KeySpec.RSA_3072,
    });

    // S3 Bucket for TUF repository
    const repositoryBucket = new s3.Bucket(this, 'TufRepository', {
      versioned: true,
      encryption: s3.BucketEncryption.KMS,
      encryptionKey: signingKey,
      publicReadAccess: true, // For public TUF repositories
    });

    // Parameter Store configuration
    new ssm.StringParameter(this, 'KmsKeyId', {
      parameterName: '/tuf/prod/kms/signing-key-id',
      stringValue: signingKey.keyId,
    });

    new ssm.StringParameter(this, 'S3Bucket', {
      parameterName: '/tuf/prod/s3/repository-bucket',
      stringValue: repositoryBucket.bucketName,
    });
  }
}
```

#### AWS CloudFormation

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Description: 'TUF .NET AWS Infrastructure'

Resources:
  TufSigningKey:
    Type: AWS::KMS::Key
    Properties:
      Description: 'TUF .NET signing key'
      KeyUsage: SIGN_VERIFY
      KeySpec: RSA_3072
      KeyPolicy:
        Statement:
          - Effect: Allow
            Principal:
              AWS: !Sub 'arn:aws:iam::${AWS::AccountId}:root'
            Action: 'kms:*'
            Resource: '*'

  TufRepositoryBucket:
    Type: AWS::S3::Bucket
    Properties:
      VersioningConfiguration:
        Status: Enabled
      BucketEncryption:
        ServerSideEncryptionConfiguration:
          - ServerSideEncryptionByDefault:
              SSEAlgorithm: aws:kms
              KMSMasterKeyID: !Ref TufSigningKey
      PublicAccessBlockConfiguration:
        BlockPublicAcls: false
        BlockPublicPolicy: false
        IgnorePublicAcls: false
        RestrictPublicBuckets: false

Outputs:
  SigningKeyId:
    Description: 'KMS Signing Key ID'
    Value: !Ref TufSigningKey
    Export:
      Name: !Sub '${AWS::StackName}-SigningKeyId'
  
  RepositoryBucket:
    Description: 'S3 Repository Bucket'
    Value: !Ref TufRepositoryBucket
    Export:
      Name: !Sub '${AWS::StackName}-RepositoryBucket'
```

### Container Deployment

#### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["examples/AwsIntegration/AwsIntegration.csproj", "examples/AwsIntegration/"]
COPY ["TUF/TUF.csproj", "TUF/"]
COPY ["CanonicalJson/CanonicalJson.csproj", "CanonicalJson/"]
RUN dotnet restore "examples/AwsIntegration/AwsIntegration.csproj"

COPY . .
WORKDIR "/src/examples/AwsIntegration"
RUN dotnet build "AwsIntegration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AwsIntegration.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install AWS CLI for credential management
RUN apt-get update && apt-get install -y awscli && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "AwsIntegration.dll"]
```

#### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tuf-aws-integration
  labels:
    app: tuf-aws-integration
spec:
  replicas: 3
  selector:
    matchLabels:
      app: tuf-aws-integration
  template:
    metadata:
      labels:
        app: tuf-aws-integration
    spec:
      serviceAccountName: tuf-service-account
      containers:
      - name: tuf-app
        image: your-registry/tuf-aws-integration:latest
        env:
        - name: AWS_REGION
          value: "us-east-1"
        - name: TUF_PARAMETER_PREFIX
          value: "/tuf/prod"
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: tuf-service-account
  annotations:
    eks.amazonaws.com/role-arn: arn:aws:iam::ACCOUNT-ID:role/TufServiceRole
```

### IAM Security Policy

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "KMSSigningPermissions",
      "Effect": "Allow",
      "Action": [
        "kms:Sign",
        "kms:GetPublicKey",
        "kms:DescribeKey"
      ],
      "Resource": "arn:aws:kms:*:*:key/*",
      "Condition": {
        "StringEquals": {
          "kms:KeyUsage": "SIGN_VERIFY"
        }
      }
    },
    {
      "Sid": "S3RepositoryPermissions",
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::your-tuf-bucket",
        "arn:aws:s3:::your-tuf-bucket/*"
      ]
    },
    {
      "Sid": "ParameterStorePermissions",
      "Effect": "Allow",
      "Action": [
        "ssm:GetParameter",
        "ssm:GetParameters",
        "ssm:GetParametersByPath",
        "ssm:PutParameter"
      ],
      "Resource": "arn:aws:ssm:*:*:parameter/tuf/*"
    }
  ]
}
```

## 🔍 Monitoring and Observability

### AWS CloudWatch Metrics

- **KMS Usage**: Sign operations, error rates, latency
- **S3 Performance**: Request metrics, data transfer, error rates  
- **Parameter Store**: GetParameter calls, cache hit rates

### AWS CloudTrail Auditing

- **KMS Operations**: All signing operations logged
- **S3 Access**: Object-level logging for compliance
- **Parameter Store**: Configuration access and changes

### Application Logging

```csharp
// Structured logging with AWS integration
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddAWSProvider(); // CloudWatch Logs integration
});

// Custom metrics
builder.Services.AddAWSService<IAmazonCloudWatch>();
```

## 💰 Cost Optimization

### KMS Costs
- **Key Storage**: $1/month per key
- **Signing Operations**: $0.03 per 10,000 operations
- **Cross-Region**: Additional charges for cross-region calls

### S3 Costs  
- **Standard Storage**: $0.023/GB/month for first 50TB
- **Data Transfer**: Free within same region, $0.09/GB out to internet
- **Requests**: $0.0004 per 1,000 PUT requests, $0.0004 per 10,000 GET requests

### Parameter Store Costs
- **Standard Parameters**: Free up to 10,000, $0.05 per 10,000 after
- **Advanced Parameters**: $0.05 per 10,000 operations
- **Throughput**: Higher throughput tiers available

### Cost Optimization Tips
1. **Use S3 Intelligent Tiering** for infrequently accessed targets
2. **Enable S3 Transfer Acceleration** only when needed
3. **Cache Parameter Store values** to reduce API calls
4. **Use CloudFront CDN** to reduce S3 data transfer costs
5. **Monitor usage** with AWS Cost Explorer and set up alerts

## 🔧 Troubleshooting

### Common Issues

#### 1. KMS Access Denied
```
Error: User: arn:aws:iam::123456789012:user/username is not authorized to perform: kms:Sign
```
**Solution**: Ensure IAM user/role has `kms:Sign` permission for the specific key.

#### 2. S3 Bucket Access Denied
```
Error: Access Denied when accessing S3 bucket
```
**Solution**: Verify bucket policy and IAM permissions for `s3:GetObject` and `s3:PutObject`.

#### 3. Parameter Not Found
```
Error: Parameter '/tuf/prod/kms/signing-key-id' not found
```
**Solution**: Run parameter setup or verify parameter path and region.

#### 4. Invalid Key Spec
```
Error: Unsupported AWS KMS key spec: ECC_NIST_P256
```
**Solution**: AWS KMS TUF integration currently supports RSA keys only.

### Debug Mode

```bash
# Enable detailed AWS SDK logging
export AWS_SDK_LOAD_CONFIG=1
export DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION=1

# Run with verbose logging
dotnet run -- full-demo --kms-key-id "your-key" --s3-bucket "your-bucket" --region us-east-1
```

## 🚀 Next Steps

1. **Security Hardening**
   - Implement least-privilege IAM policies
   - Enable MFA for sensitive operations
   - Set up AWS Config rules for compliance

2. **High Availability**
   - Multi-region KMS key replication
   - S3 Cross-Region Replication
   - Parameter Store cross-region synchronization

3. **Automation**
   - CI/CD integration for metadata updates
   - Automated key rotation workflows
   - Infrastructure as Code deployment

4. **Integration**
   - CloudFront distribution setup
   - Application Load Balancer integration
   - Container orchestration with EKS/ECS

## 📚 Additional Resources

- [TUF Specification](https://theupdateframework.io/specification/)
- [AWS KMS Best Practices](https://docs.aws.amazon.com/kms/latest/developerguide/best-practices.html)
- [S3 Security Best Practices](https://docs.aws.amazon.com/AmazonS3/latest/userguide/security-best-practices.html)
- [Parameter Store User Guide](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html)

## 🤝 Contributing

This example is part of the TUF .NET parity initiative. Contributions are welcome!

1. Follow existing code patterns and conventions
2. Include comprehensive error handling and logging  
3. Add unit tests for new functionality
4. Update documentation for any changes
5. Ensure AOT compatibility is maintained

## 📄 License

This example is part of the TUF .NET project and follows the same licensing terms.