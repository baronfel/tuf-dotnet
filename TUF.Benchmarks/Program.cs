using System;
using BenchmarkDotNet.Running;

namespace TUF.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        
        Console.WriteLine("\n=== TUF .NET Performance Benchmarks Complete ===");
        Console.WriteLine("For detailed analysis and optimization guidance, see:");
        Console.WriteLine("- docs/guides/performance.md");
        Console.WriteLine("- Performance test results in this directory");
    }
}