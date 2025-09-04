using BenchmarkDotNet.Running;

namespace TUF.PerformanceBenchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            // Run simple benchmarks by default
            BenchmarkRunner.Run<SimpleBenchmarks>();
        }
        else
        {
            // Allow running specific benchmarks via command line
            var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}