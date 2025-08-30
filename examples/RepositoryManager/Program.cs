namespace RepositoryManager;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var outputPath = args.Length > 0 ? args[0] : "./sample-tuf-repo";
            
            Console.WriteLine("TUF Repository Manager - Simple Demo");
            Console.WriteLine("=====================================");
            Console.WriteLine();
            
            // Run tests first to verify functionality
            RepositoryBuilderTest.RunTests();
            Console.WriteLine();
            
            SimpleExample.CreateSampleRepository(outputPath);
            
            Console.WriteLine();
            Console.WriteLine("✅ Demo completed successfully!");
            Console.WriteLine();
            Console.WriteLine("Next Steps:");
            Console.WriteLine($"   1. Explore the created repository at: {Path.GetFullPath(outputPath)}");
            Console.WriteLine("   2. Use the BasicClient example to test the repository");
            Console.WriteLine("   3. Examine the metadata files to understand TUF structure");
            Console.WriteLine();
            Console.WriteLine("Example usage with BasicClient:");
            Console.WriteLine($"   dotnet run --project examples/BasicClient file://{Path.GetFullPath(outputPath)}/metadata hello.txt");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}