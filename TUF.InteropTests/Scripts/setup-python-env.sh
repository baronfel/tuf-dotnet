#!/bin/bash

# Script to set up Python environment for TUF interoperability testing
# This script installs python-tuf and generates test data

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"

echo "Setting up Python-TUF interoperability test environment..."

# Check if Python is available
if ! command -v python3 &> /dev/null; then
    echo "Error: Python 3 is required but not installed."
    echo "Please install Python 3 and try again."
    exit 1
fi

# Create virtual environment if it doesn't exist
VENV_DIR="$SCRIPT_DIR/venv"
if [ ! -d "$VENV_DIR" ]; then
    echo "Creating Python virtual environment..."
    python3 -m venv "$VENV_DIR"
fi

# Activate virtual environment
source "$VENV_DIR/bin/activate"

# Upgrade pip
echo "Upgrading pip..."
pip install --upgrade pip

# Install python-tuf and dependencies
echo "Installing python-tuf and dependencies..."
pip install "tuf[signatures]>=5.0.0" cryptography

# Generate test data
echo "Generating test repository data..."
python "$SCRIPT_DIR/generate-python-tuf-test-data.py"

echo "Setup complete!"
echo ""
echo "To run the interoperability tests:"
echo "  cd $PROJECT_ROOT"
echo "  dotnet test TUF.InteropTests"
echo ""
echo "To activate the Python environment manually:"
echo "  source $VENV_DIR/bin/activate"