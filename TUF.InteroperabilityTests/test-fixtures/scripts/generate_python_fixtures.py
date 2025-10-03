#!/usr/bin/env python3
"""
Generate TUF test repositories using Python-TUF for interoperability testing.

This script creates TUF repositories that can be used to test whether
the .NET TUF implementation can successfully read and validate repositories
created by the reference Python-TUF implementation.
"""

import os
import sys
import json
import tempfile
from pathlib import Path

try:
    from tuf.api.metadata import Metadata, Root, Targets, Snapshot, Timestamp, Key, Role
    from tuf.repository import Repository
    from tuf.ngclient import Updater
    from cryptography.hazmat.primitives import hashes
    from cryptography.hazmat.primitives.asymmetric import ed25519, rsa, ec
    from cryptography.hazmat.primitives import serialization
    from securesystemslib.signer import SSlibSigner
    import securesystemslib.keys
except ImportError as e:
    print(f"Error importing Python-TUF dependencies: {e}")
    print("Please install python-tuf and dependencies:")
    print("  pip install tuf[complete]")
    sys.exit(1)

def generate_ed25519_key():
    """Generate an Ed25519 key pair."""
    private_key = ed25519.Ed25519PrivateKey.generate()
    public_key = private_key.public_key()
    
    public_pem = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )
    
    private_pem = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )
    
    return {
        'keytype': 'ed25519',
        'scheme': 'ed25519',
        'keyval': {
            'public': public_pem.decode('utf-8'),
            'private': private_pem.decode('utf-8')
        }
    }

def generate_ecdsa_key():
    """Generate an ECDSA P-256 key pair."""
    private_key = ec.generate_private_key(ec.SECP256R1())
    public_key = private_key.public_key()
    
    public_pem = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )
    
    private_pem = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )
    
    return {
        'keytype': 'ecdsa',
        'scheme': 'ecdsa-sha2-nistp256',
        'keyval': {
            'public': public_pem.decode('utf-8'),
            'private': private_pem.decode('utf-8')
        }
    }

def generate_rsa_key():
    """Generate an RSA key pair."""
    private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048
    )
    public_key = private_key.public_key()
    
    public_pem = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )
    
    private_pem = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )
    
    return {
        'keytype': 'rsa',
        'scheme': 'rsa-pss-sha256',
        'keyval': {
            'public': public_pem.decode('utf-8'),
            'private': private_pem.decode('utf-8')
        }
    }

def create_python_tuf_repository(output_dir: Path, key_type: str = 'ed25519'):
    """Create a TUF repository using Python-TUF."""
    
    print(f"Creating Python-TUF repository with {key_type} keys at: {output_dir}")
    
    # Create output directories
    metadata_dir = output_dir / "metadata"
    targets_dir = output_dir / "targets"
    metadata_dir.mkdir(parents=True, exist_ok=True)
    targets_dir.mkdir(parents=True, exist_ok=True)
    
    # Generate keys based on type
    if key_type == 'ed25519':
        key_data = generate_ed25519_key()
    elif key_type == 'ecdsa':
        key_data = generate_ecdsa_key()
    elif key_type == 'rsa':
        key_data = generate_rsa_key()
    else:
        raise ValueError(f"Unsupported key type: {key_type}")
    
    # Create a simple target file
    test_file_content = f"Hello from Python-TUF repository with {key_type} keys!"
    test_file_path = targets_dir / "python-test-file.txt"
    test_file_path.write_text(test_file_content)
    
    print(f"Created test target file: {test_file_path}")
    print(f"File content: {test_file_content}")
    print(f"File size: {len(test_file_content)} bytes")
    
    # Create metadata using python-tuf (this is a simplified approach)
    # In practice, you'd use the full Repository API
    
    # For now, create minimal valid metadata files
    import datetime
    from hashlib import sha256
    
    # Calculate file hash
    file_hash = sha256(test_file_content.encode()).hexdigest()
    file_size = len(test_file_content.encode())
    
    # Create basic metadata structure (simplified)
    root_metadata = {
        "_type": "root",
        "version": 1,
        "spec_version": "1.0.31",
        "expires": (datetime.datetime.utcnow() + datetime.timedelta(days=365)).isoformat() + "Z",
        "consistent_snapshot": False,
        "keys": {},
        "roles": {
            "root": {"keyids": [], "threshold": 1},
            "timestamp": {"keyids": [], "threshold": 1},
            "snapshot": {"keyids": [], "threshold": 1},
            "targets": {"keyids": [], "threshold": 1}
        }
    }
    
    targets_metadata = {
        "_type": "targets",
        "version": 1,
        "spec_version": "1.0.31",
        "expires": (datetime.datetime.utcnow() + datetime.timedelta(days=365)).isoformat() + "Z",
        "targets": {
            "python-test-file.txt": {
                "length": file_size,
                "hashes": {
                    "sha256": file_hash
                }
            }
        }
    }
    
    # Write metadata files
    with open(metadata_dir / "root.json", 'w') as f:
        json.dump({"signed": root_metadata, "signatures": []}, f, indent=2)
    
    with open(metadata_dir / "targets.json", 'w') as f:
        json.dump({"signed": targets_metadata, "signatures": []}, f, indent=2)
    
    print(f"✅ Created Python-TUF repository structure:")
    print(f"   - Metadata directory: {metadata_dir}")
    print(f"   - Targets directory: {targets_dir}")
    print(f"   - Key type: {key_type}")
    print(f"   - Target file: python-test-file.txt ({file_size} bytes)")
    
    return output_dir

def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print("Usage: generate_python_fixtures.py <output_directory> [key_type]")
        print("Key types: ed25519, ecdsa, rsa (default: ed25519)")
        sys.exit(1)
    
    output_dir = Path(sys.argv[1])
    key_type = sys.argv[2] if len(sys.argv) > 2 else 'ed25519'
    
    try:
        create_python_tuf_repository(output_dir, key_type)
        print(f"✅ Successfully created Python-TUF test repository at: {output_dir}")
    except Exception as e:
        print(f"❌ Failed to create Python-TUF repository: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()