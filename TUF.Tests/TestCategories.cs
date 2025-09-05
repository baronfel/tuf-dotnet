using TUnit.Core;

namespace TUF.Tests;

/// <summary>
/// Test category attributes for organizing tests into smoke vs comprehensive test runs.
/// Enables fast development cycles by running essential tests quickly.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Essential tests that must pass for basic functionality.
    /// These should run quickly (under 100ms each) and cover critical paths.
    /// </summary>
    public class SmokeTestAttribute : Attribute
    {
        public string? Description { get; set; }
        
        public SmokeTestAttribute(string? description = null)
        {
            Description = description;
        }
    }

    /// <summary>
    /// Comprehensive tests that provide thorough coverage but may take longer.
    /// These include complex scenarios, edge cases, and integration tests.
    /// </summary>
    public class ComprehensiveTestAttribute : Attribute
    {
        public string? Description { get; set; }
        
        public ComprehensiveTestAttribute(string? description = null)
        {
            Description = description;
        }
    }

    /// <summary>
    /// Performance-sensitive tests that benefit from cached data.
    /// These tests will use pre-generated cryptographic keys and test data.
    /// </summary>
    public class FastTestAttribute : Attribute
    {
        public string? Description { get; set; }
        
        public FastTestAttribute(string? description = null)
        {
            Description = description;
        }
    }
}

/// <summary>
/// Extension methods for filtering and organizing tests by category.
/// These extensions provide utilities for test categorization in development workflows.
/// </summary>
public static class TestCategoryExtensions
{
    /// <summary>
    /// Gets a text summary of all test categories defined in this system.
    /// Useful for documentation and tooling integration.
    /// </summary>
    public static string GetCategorySummary()
    {
        return @"
Test Categories Available:
- SmokeTest: Essential tests for basic functionality (should be fast)
- ComprehensiveTest: Thorough tests including edge cases and integrations  
- FastTest: Performance-optimized tests using cached data

Usage: Apply attributes like [TestCategories.SmokeTest] to test methods.
";
    }
}