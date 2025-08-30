using System.Text;
using System.Text.Json;
using TUF.Models;
using TUF.Models.Keys;
using TUF.Models.Primitives;
using TUF.Models.Roles;
using TUF.Serialization;
using TUF.Signing;

namespace SigningDemo;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("TUF .NET Signing Demonstration");
        Console.WriteLine("==============================");

        try
        {
            // Demonstrate Ed25519 signing
            await DemonstrateEd25519Signing();
            Console.WriteLine();

            // Demonstrate RSA signing
            await DemonstrateRsaSigning();
            Console.WriteLine();

            // Demonstrate key information and compatibility
            await DemonstrateKeyCompatibility();
            Console.WriteLine();

            Console.WriteLine("✅ All signing demonstrations completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static async Task DemonstrateEd25519Signing()
    {
        Console.WriteLine("🔐 Ed25519 Signing Example");
        Console.WriteLine("──────────────────────────");

        // Generate an Ed25519 key pair
        var signer = Ed25519Signer.Generate();
        Console.WriteLine($"✓ Generated Ed25519 key: {signer.Key.Id.ToJsonString()[..16]}...");

        // Sign some data
        var testMessage = "Hello, TUF World! This message is signed with Ed25519."u8.ToArray();
        var signature = signer.SignBytes(testMessage);

        Console.WriteLine($"✓ Signed {testMessage.Length} bytes of data");
        Console.WriteLine($"✓ Signature length: {signature.Value.Length} bytes");

        // Verify the signature
        var isValid = signer.Key.VerifySignature(signature.Value, testMessage);
        Console.WriteLine($"✓ Signature verification: {(isValid ? "VALID" : "INVALID")}");

        // Demonstrate that tampering breaks verification
        var tamperedMessage = "Hello, TUF World! This message has been tampered with."u8.ToArray();
        var isTamperedValid = signer.Key.VerifySignature(signature.Value, tamperedMessage);
        Console.WriteLine($"✓ Tampered message verification: {(isTamperedValid ? "VALID (UNEXPECTED!)" : "INVALID (EXPECTED)")}");
    }

    private static async Task DemonstrateRsaSigning()
    {
        Console.WriteLine("🔐 RSA-PSS Signing Example");
        Console.WriteLine("──────────────────────────");

        // Generate an RSA key pair
        var signer = RsaSigner.Generate(2048);
        Console.WriteLine($"✓ Generated RSA-PSS key: {signer.Key.Id.ToJsonString()[..16]}...");

        // Sign some data
        var testMessage = "This message demonstrates RSA-PSS signing in TUF .NET."u8.ToArray();
        var signature = signer.SignBytes(testMessage);

        Console.WriteLine($"✓ Signed {testMessage.Length} bytes of data");
        Console.WriteLine($"✓ Signature length: {signature.Value.Length} bytes");

        // Verify the signature
        var isValid = signer.Key.VerifySignature(signature.Value, testMessage);
        Console.WriteLine($"✓ Signature verification: {(isValid ? "VALID" : "INVALID")}");

        // Demonstrate different signers produce different signatures
        var signer2 = RsaSigner.Generate(2048);
        var signature2 = signer2.SignBytes(testMessage);
        Console.WriteLine($"✓ Different signers produce different signatures: {!signature.Value.SequenceEqual(signature2.Value)}");
    }

    private static async Task DemonstrateKeyCompatibility()
    {
        Console.WriteLine("🔑 TUF Key Compatibility and Information");
        Console.WriteLine("─────────────────────────────────────────");

        // Generate different types of keys
        var ed25519Signer = Ed25519Signer.Generate();
        var rsaSigner = RsaSigner.Generate(2048);
        var rsa4096Signer = RsaSigner.Generate(4096);

        Console.WriteLine("✓ Generated multiple signers:");
        Console.WriteLine($"  Ed25519 Key ID:   {ed25519Signer.Key.Id.ToJsonString()}");
        Console.WriteLine($"  RSA-2048 Key ID:  {rsaSigner.Key.Id.ToJsonString()}");
        Console.WriteLine($"  RSA-4096 Key ID:  {rsa4096Signer.Key.Id.ToJsonString()}");

        // Demonstrate key types and schemes
        Console.WriteLine("\n✓ Key algorithm information:");
        Console.WriteLine($"  Ed25519:   Type={ed25519Signer.Key.KeyType}, Scheme={ed25519Signer.Key.Scheme}");
        Console.WriteLine($"  RSA-2048:  Type={rsaSigner.Key.KeyType}, Scheme={rsaSigner.Key.Scheme}");
        Console.WriteLine($"  RSA-4096:  Type={rsa4096Signer.Key.KeyType}, Scheme={rsa4096Signer.Key.Scheme}");

        // Demonstrate cross-verification (should fail)
        var testData = "Cross-verification test data"u8.ToArray();
        var ed25519Signature = ed25519Signer.SignBytes(testData);
        var rsaSignature = rsaSigner.SignBytes(testData);

        Console.WriteLine("\n✓ Cross-algorithm verification tests:");
        Console.WriteLine($"  Ed25519 verifying RSA signature:    {ed25519Signer.Key.VerifySignature(rsaSignature.Value, testData)} (Expected: False)");
        Console.WriteLine($"  RSA verifying Ed25519 signature:    {rsaSigner.Key.VerifySignature(ed25519Signature.Value, testData)} (Expected: False)");
        
        // Demonstrate proper verification
        Console.WriteLine("\n✓ Proper algorithm verification:");
        Console.WriteLine($"  Ed25519 verifying own signature:    {ed25519Signer.Key.VerifySignature(ed25519Signature.Value, testData)} (Expected: True)");
        Console.WriteLine($"  RSA verifying own signature:        {rsaSigner.Key.VerifySignature(rsaSignature.Value, testData)} (Expected: True)");

        // Show signature sizes
        Console.WriteLine("\n✓ Signature size comparison:");
        Console.WriteLine($"  Ed25519 signature size: {ed25519Signature.Value.Length} bytes");
        Console.WriteLine($"  RSA-2048 signature size: {rsaSignature.Value.Length} bytes");
        Console.WriteLine($"  RSA-4096 signature size: {rsa4096Signer.SignBytes(testData).Value.Length} bytes");
    }
}