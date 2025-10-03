#!/usr/bin/env python3
"""
Test if Python-TUF can read and validate a repository created by .NET TUF.

This script is called from the .NET interoperability tests to verify
that repositories created by the .NET TUF implementation can be
successfully read and validated by Python-TUF.
"""

import os
import sys
import json
from pathlib import Path

try:
    from tuf.api.metadata import Metadata, Root
    from tuf.ngclient import Updater
except ImportError as e:
    print(f"Error importing Python-TUF: {e}")
    print("Please install python-tuf:")
    print("  pip install tuf[complete]")
    sys.exit(1)

def test_python_can_read_dotnet_repository(repo_path: str):
    """Test if Python-TUF can read a .NET-generated repository."""
    
    repo_path = Path(repo_path)
    print(f"Testing Python-TUF with .NET repository: {repo_path}")
    
    # Check repository structure
    metadata_dir = repo_path / "metadata"
    targets_dir = repo_path / "targets"
    
    if not metadata_dir.exists():
        print(f"❌ Metadata directory not found: {metadata_dir}")
        return False
    
    if not targets_dir.exists():
        print(f"❌ Targets directory not found: {targets_dir}")
        return False
    
    # Check for root.json
    root_path = metadata_dir / "root.json"
    if not root_path.exists():
        print(f"❌ Root metadata not found: {root_path}")
        return False
    
    try:
        # Load and parse root metadata
        with open(root_path, 'r') as f:
            root_data = json.load(f)
        
        print(f"✅ Successfully loaded root.json")
        print(f"   - File size: {root_path.stat().st_size} bytes")
        
        # Verify basic structure
        if 'signed' not in root_data:
            print("❌ Root metadata missing 'signed' field")
            return False
        
        if 'signatures' not in root_data:
            print("❌ Root metadata missing 'signatures' field")
            return False
        
        signed = root_data['signed']
        signatures = root_data['signatures']
        
        print(f"   - Type: {signed.get('_type', 'unknown')}")
        print(f"   - Version: {signed.get('version', 'unknown')}")
        print(f"   - Spec version: {signed.get('spec_version', 'unknown')}")
        print(f"   - Keys: {len(signed.get('keys', {}))}")
        print(f"   - Signatures: {len(signatures)}")
        
        # Verify it's a root metadata
        if signed.get('_type') != 'root':
            print(f"❌ Expected root metadata, got: {signed.get('_type')}")
            return False
        
        # Try to create a Metadata object (this validates the structure)
        try:
            root_metadata = Metadata.from_dict(root_data)
            print("✅ Successfully parsed root metadata using Python-TUF")
            
            # Verify it's specifically a Root metadata
            if not isinstance(root_metadata.signed, Root):
                print(f"❌ Expected Root metadata, got: {type(root_metadata.signed)}")
                return False
            
            print(f"   - Root object created successfully")
            print(f"   - Expires: {root_metadata.signed.expires}")
            print(f"   - Consistent snapshot: {root_metadata.signed.consistent_snapshot}")
            
        except Exception as e:
            print(f"❌ Failed to parse metadata with Python-TUF: {e}")
            return False
        
        # Try to list other metadata files
        metadata_files = list(metadata_dir.glob("*.json"))
        print(f"✅ Found {len(metadata_files)} metadata files:")
        for file in metadata_files:
            print(f"   - {file.name} ({file.stat().st_size} bytes)")
        
        # Try to list target files
        target_files = list(targets_dir.glob("*"))
        print(f"✅ Found {len(target_files)} target files:")
        for file in target_files:
            if file.is_file():
                print(f"   - {file.name} ({file.stat().st_size} bytes)")
        
        print("✅ Python-TUF successfully validated .NET repository structure")
        return True
        
    except Exception as e:
        print(f"❌ Error processing .NET repository: {e}")
        import traceback
        traceback.print_exc()
        return False

def main():
    """Main entry point."""
    if len(sys.argv) != 2:
        print("Usage: test_python_can_read_dotnet.py <repository_path>")
        sys.exit(1)
    
    repo_path = sys.argv[1]
    
    if not os.path.exists(repo_path):
        print(f"❌ Repository path does not exist: {repo_path}")
        sys.exit(1)
    
    success = test_python_can_read_dotnet_repository(repo_path)
    
    if success:
        print("🎉 SUCCESS: Python-TUF can read .NET TUF repository")
        sys.exit(0)
    else:
        print("💥 FAILURE: Python-TUF cannot read .NET TUF repository")
        sys.exit(1)

if __name__ == "__main__":
    main()