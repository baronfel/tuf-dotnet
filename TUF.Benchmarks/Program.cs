using BenchmarkDotNet.Running;
using TUF.Benchmarks;

namespace TUF.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}