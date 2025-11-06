#!/usr/bin/env python3
"""
TUF .NET Conformance Test Runner

This script provides a standalone way to run TUF conformance tests against the .NET implementation.
It's designed to be used both in CI and for local development.
"""

import argparse
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path


def run_command(cmd, cwd=None, check=True):
    """Run a shell command and return the result."""
    print(f"Running: {' '.join(cmd) if isinstance(cmd, list) else cmd}")
    result = subprocess.run(
        cmd,
        cwd=cwd,
        shell=isinstance(cmd, str),
        capture_output=True,
        text=True
    )
    
    if check and result.returncode != 0:
        print(f"Command failed with exit code {result.returncode}")
        print(f"STDOUT: {result.stdout}")
        print(f"STDERR: {result.stderr}")
        return None
    
    return result


def setup_conformance_environment():
    """Set up the TUF conformance test environment."""
    repo_root = Path(__file__).parent.parent
    
    print("🏗️  Setting up conformance test environment...")
    
    # Build the TUF conformance CLI
    build_result = run_command([
        "dotnet", "build", 
        str(repo_root / "examples" / "TufConformanceCli" / "TufConformanceCli.csproj"),
        "--configuration", "Release"
    ])
    
    if not build_result:
        print("❌ Failed to build TUF conformance CLI")
        return False
    
    # Test that CLI is working
    cli_test = run_command([
        "dotnet", "run", 
        "--project", str(repo_root / "examples" / "TufConformanceCli" / "TufConformanceCli.csproj"),
        "--", "--help"
    ], check=False)
    
    if not cli_test or cli_test.returncode != 0:
        print("❌ TUF conformance CLI is not responding properly")
        return False
    
    print("✅ TUF conformance CLI built and verified")
    return True


def check_conformance_suite():
    """Check if the TUF conformance test suite is available."""
    print("🔍 Checking for TUF conformance test suite...")
    
    # Try to clone the conformance suite if not present
    conformance_dir = Path("tuf-conformance")
    if not conformance_dir.exists():
        print("📦 Cloning TUF conformance test suite...")
        clone_result = run_command([
            "git", "clone", 
            "https://github.com/theupdateframework/tuf-conformance.git"
        ])
        
        if not clone_result:
            print("❌ Failed to clone TUF conformance suite")
            return False
    
    # Install conformance test dependencies
    print("📦 Installing conformance test dependencies...")
    install_result = run_command([
        "pip", "install", "-e", "."
    ], cwd=conformance_dir)
    
    if not install_result:
        print("❌ Failed to install conformance test dependencies")
        return False
        
    print("✅ TUF conformance test suite ready")
    return True


def run_conformance_tests():
    """Run the actual TUF conformance tests."""
    repo_root = Path(__file__).parent.parent
    conformance_dir = Path("tuf-conformance")
    
    print("🧪 Running TUF conformance tests...")
    
    # Create conformance configuration
    config_content = f"""
name: tuf-dotnet
implementation:
  command: ["dotnet", "run", "--project", "{repo_root}/examples/TufConformanceCli/TufConformanceCli.csproj", "--"]
  version: "0.1.0-dev"
  language: "C#"
"""
    
    config_path = Path("conformance-config.yml")
    with open(config_path, 'w') as f:
        f.write(config_content)
    
    # Run conformance tests
    cmd = [
        "python", "-m", "conformance",
        "--implementation-config", str(config_path.absolute()),
        "--output-file", str(repo_root / "conformance-results.json"),
        "--expected-failures", str(repo_root / ".tuf-conformance-xfails")
    ]
    
    test_result = run_command(cmd, cwd=conformance_dir, check=False)
    
    return test_result.returncode if test_result else 1


def analyze_results():
    """Analyze and report conformance test results."""
    repo_root = Path(__file__).parent.parent
    results_file = repo_root / "conformance-results.json"
    
    if not results_file.exists():
        print("❌ No conformance results found")
        return
    
    print("📊 Analyzing conformance test results...")
    
    try:
        with open(results_file) as f:
            results = json.load(f)
        
        tests = results.get('tests', [])
        total = len(tests)
        passed = len([t for t in tests if t.get('result') == 'pass'])
        failed = len([t for t in tests if t.get('result') == 'fail'])
        skipped = len([t for t in tests if t.get('result') == 'skip'])
        
        print(f"\n🎯 TUF .NET Conformance Test Results:")
        print(f"   ✅ Passed:  {passed:3d}")
        print(f"   ❌ Failed:  {failed:3d}")
        print(f"   ⏭️  Skipped: {skipped:3d}")
        print(f"   📊 Total:   {total:3d}")
        
        if total > 0:
            success_rate = passed / total * 100
            print(f"   🎯 Success Rate: {success_rate:.1f}%")
        
        if failed > 0:
            print(f"\n❌ Failed Tests (first 10):")
            failed_tests = [t for t in tests if t.get('result') == 'fail'][:10]
            for i, test in enumerate(failed_tests, 1):
                test_name = test.get('name', 'unknown')
                print(f"   {i:2d}. {test_name}")
            
            if len([t for t in tests if t.get('result') == 'fail']) > 10:
                remaining = len([t for t in tests if t.get('result') == 'fail']) - 10
                print(f"       ... and {remaining} more")
        
        # Suggest updating .tuf-conformance-xfails if there are consistent failures
        if failed > 0:
            print(f"\n💡 Consider updating .tuf-conformance-xfails with these failing tests")
            print(f"   to track progress as the implementation improves.")
            
        return failed == 0
        
    except Exception as e:
        print(f"❌ Error analyzing results: {e}")
        return False


def main():
    """Main entry point for the conformance test runner."""
    parser = argparse.ArgumentParser(
        description="Run TUF conformance tests against the .NET implementation"
    )
    parser.add_argument(
        '--setup-only', 
        action='store_true',
        help='Only set up the environment, do not run tests'
    )
    parser.add_argument(
        '--analyze-only',
        action='store_true', 
        help='Only analyze existing results, do not run tests'
    )
    
    args = parser.parse_args()
    
    if args.analyze_only:
        success = analyze_results()
        sys.exit(0 if success else 1)
    
    # Setup environment
    if not setup_conformance_environment():
        print("❌ Failed to set up conformance test environment")
        sys.exit(1)
    
    if args.setup_only:
        print("✅ Conformance test environment set up successfully")
        sys.exit(0)
    
    # Set up conformance test suite
    if not check_conformance_suite():
        print("❌ Failed to set up TUF conformance test suite")
        sys.exit(1)
    
    # Run the tests
    exit_code = run_conformance_tests()
    
    # Analyze results regardless of test outcome
    analyze_results()
    
    if exit_code == 0:
        print("🎉 All conformance tests passed!")
    else:
        print("⚠️  Some conformance tests failed - this is normal during development")
    
    sys.exit(exit_code)


if __name__ == "__main__":
    main()