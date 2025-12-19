using BenchmarkDotNet.Running;

namespace TUF.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SimpleBenchmarks>();
    }
}