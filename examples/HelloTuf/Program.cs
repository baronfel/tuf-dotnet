using System;
using System.Net.Http;

using TUF;
using TUF.Exceptions;

Console.WriteLine("🔒 Hello, TUF!");

Console.WriteLine("\n✨ This is what TUF does for you:");
Console.WriteLine("   🛡️  Cryptographic verification of all downloads");
Console.WriteLine("   🚫  Prevents malware injection and tampering");
Console.WriteLine("   ⏮️   Blocks rollback to vulnerable versions");
Console.WriteLine("   ⏰  Ensures metadata freshness");
Console.WriteLine("   🔐  Multi-key security (compromise resistance)");

Console.WriteLine("\n📋 Here's the minimal TUF workflow:");

Console.WriteLine("\n1️⃣  Create TUF client configuration:");
Console.WriteLine("   var trustedRoot = GetEmbeddedTrustedRoot();  // From your app bundle");
Console.WriteLine("   var config = new UpdaterConfig(trustedRoot, repoUrl);");
Console.WriteLine("   var updater = new Updater(config);");

Console.WriteLine("\n2️⃣  Refresh metadata (TUF does ALL the security checking):");
Console.WriteLine("   await updater.RefreshAsync();");
Console.WriteLine("   // ✅ Verifies signatures, detects attacks, checks expiry");

Console.WriteLine("\n3️⃣  Find and download files securely:");
Console.WriteLine("   var (path, info) = await updater.GetTargetInfo(\"my-app.exe\");");
Console.WriteLine("   var (localPath, data) = await updater.DownloadTarget(info, path);");
Console.WriteLine("   // ✅ File is cryptographically verified and safe to use!");

Console.WriteLine("\n🎯 Error handling catches security threats:");
Console.WriteLine("   try { /* TUF operations */ }");
Console.WriteLine("   catch (TufException ex) {");
Console.WriteLine("       // TUF detected something suspicious - STOP!");
Console.WriteLine("       // Could be: tampering, rollback, expired metadata, etc.");
Console.WriteLine("   }");

Console.WriteLine("\n🔄 Real-world example:");
ShowRealWorldExample();

Console.WriteLine("\n🎉 That's it! TUF handles all the security complexity.");
Console.WriteLine("    Your app just downloads files normally, but they're automatically");
Console.WriteLine("    protected against tampering, rollbacks, and other attacks.");

static void ShowRealWorldExample()
{
    Console.WriteLine("   // Check for app updates");
    Console.WriteLine("   await updater.RefreshAsync();");
    Console.WriteLine("   var update = await updater.GetTargetInfo(\"MyApp-v2.0.exe\");");
    Console.WriteLine("   if (update.HasValue) {");
    Console.WriteLine("       var (path, data) = await updater.DownloadTarget(");
    Console.WriteLine("           update.Value.File, update.Value.Path);");
    Console.WriteLine("       InstallUpdate(path);  // Safe to install!");
    Console.WriteLine("   }");
}