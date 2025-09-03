using System.Diagnostics;

namespace TUF.Tests.TestFixtures;

/// <summary>
/// Simple performance measurement utility for tests
/// </summary>
public static class PerformanceMeasurement
{
    /// <summary>
    /// Measures the execution time of an action
    /// </summary>
    public static TimeSpan Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Measures the execution time of an async action
    /// </summary>
    public static async Task<TimeSpan> MeasureAsync(Func<Task> asyncAction)
    {
        var stopwatch = Stopwatch.StartNew();
        await asyncAction();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Runs an action multiple times and returns statistics
    /// </summary>
    public static (TimeSpan Min, TimeSpan Max, TimeSpan Average, TimeSpan[] AllTimes) MeasureMultiple(Action action, int iterations = 10)
    {
        var times = new TimeSpan[iterations];
        
        for (int i = 0; i < iterations; i++)
        {
            times[i] = Measure(action);
        }

        return (
            Min: times.Min(),
            Max: times.Max(), 
            Average: new TimeSpan((long)times.Average(t => t.Ticks)),
            AllTimes: times
        );
    }

    /// <summary>
    /// Compare performance of two actions
    /// </summary>
    public static (TimeSpan BaselineAverage, TimeSpan OptimizedAverage, double ImprovementRatio) Compare(
        Action baseline, Action optimized, int iterations = 10)
    {
        var baselineStats = MeasureMultiple(baseline, iterations);
        var optimizedStats = MeasureMultiple(optimized, iterations);

        var improvementRatio = baselineStats.Average.TotalMilliseconds / optimizedStats.Average.TotalMilliseconds;

        return (baselineStats.Average, optimizedStats.Average, improvementRatio);
    }
}