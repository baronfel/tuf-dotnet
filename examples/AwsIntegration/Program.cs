using Amazon.KeyManagementService;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;

using AwsIntegration;

namespace AwsIntegration;

/// <summary>
/// AWS integration demonstration for TUF .NET showing enterprise cloud deployment patterns.
/// This example demonstrates AWS KMS signing, S3 storage, and Parameter Store configuration.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 TUF .NET AWS Integration Demonstration");
        Console.WriteLine("==========================================");
        
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        
        try
        {
            return command switch
            {
                "kms-demo" => await RunKmsDemoAsync(args.Skip(1).ToArray()),
                "s3-demo" => await RunS3DemoAsync(args.Skip(1).ToArray()),
                "params-demo" => await RunParameterStoreDemoAsync(args.Skip(1).ToArray()),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => ShowHelp("Unknown command: " + command)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static int ShowHelp(string? error = null)
    {
        if (error != null)
        {
            Console.WriteLine($"❌ {error}");
            Console.WriteLine();
        }

        Console.WriteLine("Usage: AwsIntegration <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  kms-demo <key-id> [region]     - Demonstrate AWS KMS signing");
        Console.WriteLine("  s3-demo <bucket> [region]      - Demonstrate S3 repository operations"); 
        Console.WriteLine("  params-demo <prefix> [region]  - Demonstrate Parameter Store configuration");
        Console.WriteLine("  help                           - Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  AwsIntegration kms-demo alias/my-tuf-key us-east-1");
        Console.WriteLine("  AwsIntegration s3-demo my-tuf-bucket us-west-2");
        Console.WriteLine("  AwsIntegration params-demo /tuf/demo us-east-1");
        Console.WriteLine();
        Console.WriteLine("Requirements:");
        Console.WriteLine("  - AWS credentials configured (via AWS CLI, environment, or IAM roles)");
        Console.WriteLine("  - Appropriate IAM permissions for KMS, S3, and SSM operations");

        return error != null ? 1 : 0;
    }

    private static async Task<int> RunKmsDemoAsync(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("❌ KMS key ID required");
            Console.WriteLine("Usage: AwsIntegration kms-demo <key-id> [region]");
            return 1;
        }

        var keyId = args[0];
        var region = args.Length > 1 ? args[1] : "us-east-1";

        Console.WriteLine("🔐 AWS KMS Integration Demonstration");
        Console.WriteLine($"📍 Region: {region}");
        Console.WriteLine($"🔑 Key ID: {keyId}");
        Console.WriteLine();

        using var kmsClient = new AmazonKeyManagementServiceClient(Amazon.RegionEndpoint.GetBySystemName(region));
        
        try
        {
            // Create KMS signer
            Console.WriteLine("📊 Creating AWS KMS signer...");
            using var signer = await AwsKmsSigner.CreateAsync(kmsClient, keyId);
            
            Console.WriteLine("✅ KMS signer created successfully!");
            Console.WriteLine($"   TUF Key ID: {signer.Key.Id.Value}");
            Console.WriteLine($"   Key Type: {signer.Key.Type}");
            Console.WriteLine($"   Scheme: {signer.Key.Scheme}");
            
            // Test signing
            Console.WriteLine();
            Console.WriteLine("🖊️  Testing signature operation...");
            var testData = "Hello from TUF .NET AWS Integration!"u8.ToArray();
            var signature = signer.SignBytes(testData);
            
            Console.WriteLine("✅ Message signed successfully with AWS KMS!");
            Console.WriteLine($"   Signature Key ID: {signature.KeyId.Value}");
            Console.WriteLine($"   Signature Length: {signature.Sig.Length} bytes");
            Console.WriteLine($"   Signature Preview: {Convert.ToHexString(signature.Sig)[..32]}...");
            
            Console.WriteLine();
            Console.WriteLine("🎯 AWS KMS integration demonstration complete!");
            Console.WriteLine("   ✓ Connected to AWS KMS successfully");
            Console.WriteLine("   ✓ Retrieved public key information");
            Console.WriteLine("   ✓ Performed HSM-backed signing operation");
            Console.WriteLine("   ✓ Generated valid TUF signature object");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AWS KMS operation failed: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("🔧 Troubleshooting:");
            Console.WriteLine("   1. Verify AWS credentials are configured");
            Console.WriteLine("   2. Check IAM permissions for KMS operations");
            Console.WriteLine("   3. Ensure KMS key exists and is enabled");
            Console.WriteLine("   4. Verify key is configured for SIGN_VERIFY usage");
            return 1;
        }
    }

    private static async Task<int> RunS3DemoAsync(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("❌ S3 bucket name required");
            Console.WriteLine("Usage: AwsIntegration s3-demo <bucket> [region]");
            return 1;
        }

        var bucketName = args[0];
        var region = args.Length > 1 ? args[1] : "us-east-1";

        Console.WriteLine("📦 AWS S3 Repository Integration Demonstration");
        Console.WriteLine($"📍 Region: {region}");
        Console.WriteLine($"🪣 Bucket: {bucketName}");
        Console.WriteLine();

        using var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));
        using var s3Repository = new S3RepositoryBackend(s3Client, bucketName);
        
        try
        {
            // Validate access
            Console.WriteLine("🔍 Validating S3 bucket access...");
            var hasAccess = await s3Repository.ValidateAccessAsync();
            if (!hasAccess)
            {
                Console.WriteLine("❌ Cannot access S3 bucket");
                Console.WriteLine("   Check bucket name, region, and AWS credentials");
                return 1;
            }
            
            Console.WriteLine("✅ S3 bucket access validated!");
            
            // Upload sample metadata
            Console.WriteLine();
            Console.WriteLine("⬆️  Uploading sample TUF metadata...");
            var sampleMetadata = """
            {
              "_type": "timestamp",
              "spec_version": "1.0.0",
              "version": 1,
              "expires": "2025-12-31T23:59:59Z",
              "meta": {
                "snapshot.json": {
                  "version": 1,
                  "length": 1024,
                  "hashes": {
                    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                  }
                }
              }
            }
            """;
            
            await s3Repository.UploadMetadataAsync("timestamp.json", sampleMetadata);
            Console.WriteLine("   ✓ timestamp.json uploaded to S3");
            
            // Upload sample target
            Console.WriteLine();
            Console.WriteLine("📁 Uploading sample target file...");
            var sampleTarget = System.Text.Encoding.UTF8.GetBytes(
                $"TUF AWS Integration Demo\nGenerated: {DateTimeOffset.UtcNow:O}\nBucket: {bucketName}"
            );
            
            using var targetStream = new MemoryStream(sampleTarget);
            await s3Repository.UploadTargetAsync("demo/sample.txt", targetStream, "text/plain");
            Console.WriteLine("   ✓ demo/sample.txt uploaded to S3");
            
            // List metadata
            Console.WriteLine();
            Console.WriteLine("📋 Listing TUF metadata in repository...");
            var metadataFiles = await s3Repository.ListMetadataAsync();
            Console.WriteLine($"   Found {metadataFiles.Count} metadata files:");
            foreach (var file in metadataFiles)
            {
                Console.WriteLine($"     - {file}");
            }
            
            Console.WriteLine();
            Console.WriteLine("🎯 S3 repository integration demonstration complete!");
            Console.WriteLine("   ✓ Validated S3 bucket access");
            Console.WriteLine("   ✓ Uploaded TUF metadata with proper content type");
            Console.WriteLine("   ✓ Uploaded target files with integrity metadata");
            Console.WriteLine("   ✓ Listed repository contents");
            Console.WriteLine();
            Console.WriteLine($"📋 Repository Structure:");
            Console.WriteLine($"   s3://{bucketName}/metadata/timestamp.json");
            Console.WriteLine($"   s3://{bucketName}/targets/demo/sample.txt");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ S3 operation failed: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("🔧 Troubleshooting:");
            Console.WriteLine("   1. Verify AWS credentials are configured");
            Console.WriteLine("   2. Check S3 bucket exists and is accessible");
            Console.WriteLine("   3. Ensure IAM permissions for S3 operations");
            Console.WriteLine("   4. Verify bucket region matches specified region");
            return 1;
        }
    }

    private static async Task<int> RunParameterStoreDemoAsync(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("❌ Parameter prefix required");
            Console.WriteLine("Usage: AwsIntegration params-demo <prefix> [region]");
            return 1;
        }

        var prefix = args[0];
        var region = args.Length > 1 ? args[1] : "us-east-1";

        Console.WriteLine("⚙️  AWS Parameter Store Integration Demonstration");
        Console.WriteLine($"📍 Region: {region}");
        Console.WriteLine($"📋 Prefix: {prefix}");
        Console.WriteLine();

        using var ssmClient = new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.GetBySystemName(region));
        using var paramConfig = new ParameterStoreConfig(ssmClient, prefix);
        
        try
        {
            // Set up demo parameters
            Console.WriteLine("📝 Setting up demo TUF configuration parameters...");
            
            await paramConfig.SetParameterAsync("kms/region", region);
            Console.WriteLine("   ✓ kms/region");
            
            await paramConfig.SetParameterAsync("kms/signing-key-id", "alias/tuf-demo-key");
            Console.WriteLine("   ✓ kms/signing-key-id");
            
            await paramConfig.SetParameterAsync("s3/repository-bucket", "tuf-demo-repository");
            Console.WriteLine("   ✓ s3/repository-bucket");
            
            await paramConfig.SetParameterAsync("repository/name", "demo");
            Console.WriteLine("   ✓ repository/name");
            
            // Retrieve configuration
            Console.WriteLine();
            Console.WriteLine("🔄 Loading TUF configuration from Parameter Store...");
            
            var config = await paramConfig.GetTufConfigurationAsync();
            
            Console.WriteLine("✅ Configuration loaded successfully!");
            Console.WriteLine();
            Console.WriteLine("📋 TUF Configuration:");
            Console.WriteLine("──────────────────────");
            Console.WriteLine($"🔐 KMS Key ID: {config.KmsKeyId}");
            Console.WriteLine($"🔐 KMS Region: {config.KmsRegion}");
            Console.WriteLine($"📦 S3 Bucket: {config.S3BucketName}");
            Console.WriteLine($"🏛️ Repository: {config.RepositoryName}");
            Console.WriteLine($"⏰ Root Expiry: {config.RootExpiryDays} days");
            Console.WriteLine($"⚙️ Log Level: {config.LogLevel}");
            
            Console.WriteLine();
            Console.WriteLine("🎯 Parameter Store integration demonstration complete!");
            Console.WriteLine("   ✓ Created hierarchical parameter structure");
            Console.WriteLine("   ✓ Stored configuration with proper descriptions");
            Console.WriteLine("   ✓ Retrieved and parsed configuration object");
            Console.WriteLine("   ✓ Demonstrated centralized configuration management");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Parameter Store operation failed: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("🔧 Troubleshooting:");
            Console.WriteLine("   1. Verify AWS credentials are configured");
            Console.WriteLine("   2. Check IAM permissions for SSM operations");
            Console.WriteLine("   3. Ensure parameter prefix is valid");
            Console.WriteLine("   4. Verify region is correct");
            return 1;
        }
    }
}