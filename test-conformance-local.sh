#!/bin/bash

# TUF .NET Local Conformance Test Runner
# 
# This script provides a convenient way to run local conformance tests
# that mirror the official TUF conformance test suite.

set -e  # Exit on any error

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m' 
CYAN='\033[0;36m'
GRAY='\033[0;37m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Parse arguments
TEST=""
VERBOSE=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --test)
      TEST="$2"
      shift 2
      ;;
    --verbose)
      VERBOSE=true
      shift
      ;;
    -h|--help)
      echo "Usage: $0 [--test TEST_NAME] [--verbose] [--help]"
      echo ""
      echo "Options:"
      echo "  --test TEST_NAME    Run specific test"
      echo "  --verbose          Enable verbose output"  
      echo "  --help             Show this help message"
      echo ""
      echo "Examples:"
      echo "  $0                                    Run all local conformance tests"
      echo "  $0 --test Test_Init_Command --verbose Run specific test with verbose output"
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

echo -e "${GREEN}TUF .NET Local Conformance Test Runner${NC}"
echo -e "${GREEN}=====================================${NC}\n"

# Build the conformance CLI first
echo -e "${YELLOW}Building TUF Conformance CLI...${NC}"
if ! dotnet build examples/TufConformanceCli/TufConformanceCli.csproj --configuration Release --verbosity minimal; then
    echo -e "${RED}Failed to build TUF Conformance CLI${NC}"
    exit 1
fi
echo -e "${GREEN}✓ TUF Conformance CLI built successfully${NC}\n"

# Build the test project
echo -e "${YELLOW}Building test project...${NC}"
if ! dotnet build TUF.Tests/TUF.Tests.csproj --configuration Release --verbosity minimal; then
    echo -e "${RED}Failed to build test project${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Test project built successfully${NC}\n"

# Prepare test command
TEST_ARGS=("test" "TUF.Tests/TUF.Tests.csproj" "--configuration" "Release" "--no-build")

if [[ -n "$TEST" ]]; then
    TEST_ARGS+=("--filter" "Name~$TEST")
    echo -e "${CYAN}Running specific test: $TEST${NC}"
else
    TEST_ARGS+=("--filter" "FullyQualifiedName~ConformanceTests")
    echo -e "${CYAN}Running all local conformance tests...${NC}"
fi

if [[ "$VERBOSE" == true ]]; then
    TEST_ARGS+=("--verbosity" "detailed")
fi

echo -e "\n${GRAY}Test command: dotnet ${TEST_ARGS[*]}${NC}\n"

# Run the tests
export DOTNET_ENVIRONMENT=Test
set +e  # Don't exit on test failures, we want to show the summary
dotnet "${TEST_ARGS[@]}"
EXIT_CODE=$?
set -e

if [[ $EXIT_CODE -eq 0 ]]; then
    echo -e "\n${GREEN}✓ All tests completed successfully${NC}"
else
    echo -e "\n${YELLOW}⚠ Some tests failed - this is expected during development${NC}"
    echo -e "${YELLOW}Check the test output above for specific errors and debugging information${NC}"
fi

echo -e "\n${CYAN}Debugging Tips:${NC}"
echo "- Tests create temporary directories with server metadata for inspection" 
echo "- HTTP server runs on localhost:8080 during tests"
echo "- Check CLI output for specific signature validation errors"
echo "- Use --verbose flag for detailed test execution logs"

exit $EXIT_CODE