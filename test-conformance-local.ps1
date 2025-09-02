#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Local TUF conformance test runner for debugging

.DESCRIPTION
    This script provides a convenient way to run local conformance tests
    that mirror the official TUF conformance test suite. It builds the
    conformance CLI and runs the local test infrastructure.

.PARAMETER Test
    Specific test name to run (optional)

.PARAMETER Verbose
    Enable verbose output for debugging

.EXAMPLE
    ./test-conformance-local.ps1
    Run all local conformance tests

.EXAMPLE
    ./test-conformance-local.ps1 -Test "Test_Init_Command" -Verbose
    Run specific test with verbose output
#>

param(
    [string]$Test = "",
    [switch]$Verbose
)

Write-Host "TUF .NET Local Conformance Test Runner" -ForegroundColor Green
Write-Host "=====================================`n" -ForegroundColor Green

# Build the conformance CLI first
Write-Host "Building TUF Conformance CLI..." -ForegroundColor Yellow
$buildResult = dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build TUF Conformance CLI"
    exit 1
}
Write-Host "✓ TUF Conformance CLI built successfully`n" -ForegroundColor Green

# Build the test project
Write-Host "Building test project..." -ForegroundColor Yellow
$testBuildResult = dotnet build TUF.Tests/TUF.Tests.csproj --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build test project"
    exit 1
}
Write-Host "✓ Test project built successfully`n" -ForegroundColor Green

# Prepare test command
$testArgs = @("test", "TUF.Tests/TUF.Tests.csproj", "--configuration", "Release", "--no-build")

if ($Test) {
    $testArgs += "--filter"
    $testArgs += "Name~$Test"
    Write-Host "Running specific test: $Test" -ForegroundColor Cyan
} else {
    $testArgs += "--filter"
    $testArgs += "FullyQualifiedName~ConformanceTests"
    Write-Host "Running all local conformance tests..." -ForegroundColor Cyan
}

if ($Verbose) {
    $testArgs += "--verbosity"
    $testArgs += "detailed"
}

Write-Host "`nTest command: dotnet $($testArgs -join ' ')`n" -ForegroundColor Gray

# Run the tests
$env:DOTNET_ENVIRONMENT = "Test"
& dotnet @testArgs

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "`n✓ All tests completed successfully" -ForegroundColor Green
} else {
    Write-Host "`n⚠ Some tests failed - this is expected during development" -ForegroundColor Yellow
    Write-Host "Check the test output above for specific errors and debugging information" -ForegroundColor Yellow
}

Write-Host "`nDebugging Tips:" -ForegroundColor Cyan
Write-Host "- Tests create temporary directories with server metadata for inspection" 
Write-Host "- HTTP server runs on localhost:8080 during tests"
Write-Host "- Check CLI output for specific signature validation errors"
Write-Host "- Use -Verbose flag for detailed test execution logs"

exit $exitCode