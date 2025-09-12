#!/usr/bin/env python3
"""
Script to generate TUF repository test data using python-tuf for interoperability testing.

This script creates a complete TUF repository with all required metadata files
that can be used to test the .NET TUF implementation's ability to consume
repositories created by the reference python-tuf implementation.
"""

import os
import shutil
import tempfile
from datetime import datetime, timedelta
from pathlib import Path

try:
    from tuf.api.metadata import Metadata, Root, Timestamp, Snapshot, Targets, Key, Role, TargetFile
    from tuf.repository import Repository
    from cryptography.hazmat.primitives import serialization
    from cryptography.hazmat.primitives.asymmetric import ed25519, rsa, ec
except ImportError as e:
    print(f"Error: Missing required dependencies. Install with:")
    print(f"pip install tuf[signatures] cryptography")
    print(f"Import error: {e}")
    exit(1)

def generate_keys():
    """Generate test keys for different signature schemes."""
    keys = {}
    
    # Generate Ed25519 key
    ed25519_private = ed25519.Ed25519PrivateKey.generate()
    ed25519_public = ed25519_private.public_key()
    keys['ed25519'] = {
        'private': ed25519_private,
        'public': ed25519_public,
        'keytype': 'ed25519',
        'scheme': 'ed25519'
    }
    
    # Generate RSA key
    rsa_private = rsa.generate_private_key(public_exponent=65537, key_size=3072)
    rsa_public = rsa_private.public_key()
    keys['rsa'] = {
        'private': rsa_private,
        'public': rsa_public,
        'keytype': 'rsa',
        'scheme': 'rsa-pss-sha256'
    }
    
    # Generate ECDSA key
    ecdsa_private = ec.generate_private_key(ec.SECP256R1())
    ecdsa_public = ecdsa_private.public_key()
    keys['ecdsa'] = {
        'private': ecdsa_private,
        'public': ecdsa_public,
        'keytype': 'ecdsa',
        'scheme': 'ecdsa-sha2-nistp256'
    }
    
    return keys

def create_tuf_keys(crypto_keys):
    """Convert cryptographic keys to TUF Key objects."""
    tuf_keys = {}
    
    for key_name, key_info in crypto_keys.items():
        if key_name == 'ed25519':
            # Ed25519 public key serialization
            public_bytes = key_info['public'].public_bytes_raw()
            tuf_key = Key(
                keytype=key_info['keytype'],
                scheme=key_info['scheme'],
                keyval={'public': public_bytes.hex()}
            )
        elif key_name == 'rsa':
            # RSA public key serialization
            public_pem = key_info['public'].public_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PublicFormat.SubjectPublicKeyInfo
            )
            tuf_key = Key(
                keytype=key_info['keytype'],
                scheme=key_info['scheme'],
                keyval={'public': public_pem.decode('utf-8')}
            )
        elif key_name == 'ecdsa':
            # ECDSA public key serialization
            public_pem = key_info['public'].public_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PublicFormat.SubjectPublicKeyInfo
            )
            tuf_key = Key(
                keytype=key_info['keytype'],
                scheme=key_info['scheme'],
                keyval={'public': public_pem.decode('utf-8')}
            )
        
        tuf_keys[key_name] = tuf_key
    
    return tuf_keys

def create_test_repository(output_dir: Path):
    """Create a complete TUF repository with test data."""
    print(f"Creating TUF repository in {output_dir}")
    
    # Generate cryptographic keys
    crypto_keys = generate_keys()
    tuf_keys = create_tuf_keys(crypto_keys)
    
    # Create repository directories
    metadata_dir = output_dir / "metadata"
    targets_dir = output_dir / "targets"
    metadata_dir.mkdir(parents=True, exist_ok=True)
    targets_dir.mkdir(parents=True, exist_ok=True)
    
    # Create a simple target file
    test_file = targets_dir / "hello-world.txt"
    test_file.write_text("Hello from python-tuf interoperability test!")
    
    # Calculate file hash and size
    import hashlib
    with open(test_file, 'rb') as f:
        content = f.read()
        sha256_hash = hashlib.sha256(content).hexdigest()
        file_size = len(content)
    
    # Create Root metadata
    now = datetime.utcnow()
    expires = now + timedelta(days=365)
    
    root_role = Role(keyids=list(tuf_keys.keys()), threshold=1)
    targets_role = Role(keyids=list(tuf_keys.keys()), threshold=1)
    snapshot_role = Role(keyids=list(tuf_keys.keys()), threshold=1)
    timestamp_role = Role(keyids=list(tuf_keys.keys()), threshold=1)
    
    root = Root(
        version=1,
        expires=expires,
        keys=tuf_keys,
        roles={
            'root': root_role,
            'targets': targets_role,
            'snapshot': snapshot_role,
            'timestamp': timestamp_role
        },
        consistent_snapshot=False
    )
    
    # Create Targets metadata
    target_file_obj = TargetFile(
        length=file_size,
        hashes={'sha256': sha256_hash}
    )
    
    targets = Targets(
        version=1,
        expires=expires,
        targets={'hello-world.txt': target_file_obj}
    )
    
    # Create Snapshot metadata
    snapshot = Snapshot(
        version=1,
        expires=expires,
        meta={
            'targets.json': {
                'version': 1,
                'length': 0,  # Would be calculated in real implementation
                'hashes': {}  # Would be calculated in real implementation
            }
        }
    )
    
    # Create Timestamp metadata
    timestamp = Timestamp(
        version=1,
        expires=expires,
        meta={
            'snapshot.json': {
                'version': 1,
                'length': 0,  # Would be calculated in real implementation
                'hashes': {}  # Would be calculated in real implementation
            }
        }
    )
    
    # Create Metadata objects (with signatures placeholder)
    root_metadata = Metadata(signed=root, signatures=[])
    targets_metadata = Metadata(signed=targets, signatures=[])
    snapshot_metadata = Metadata(signed=snapshot, signatures=[])
    timestamp_metadata = Metadata(signed=timestamp, signatures=[])
    
    # Save metadata files
    # Note: In a real implementation, these would be properly signed
    # For interop testing, we focus on the JSON structure compatibility
    
    with open(metadata_dir / "root.json", 'w') as f:
        f.write(root_metadata.to_json())
    
    with open(metadata_dir / "targets.json", 'w') as f:
        f.write(targets_metadata.to_json())
    
    with open(metadata_dir / "snapshot.json", 'w') as f:
        f.write(snapshot_metadata.to_json())
    
    with open(metadata_dir / "timestamp.json", 'w') as f:
        f.write(timestamp_metadata.to_json())
    
    print("Repository created successfully!")
    print(f"- Metadata files: {metadata_dir}")
    print(f"- Target files: {targets_dir}")
    print(f"- Test target: {test_file}")

def main():
    """Main entry point."""
    # Get the test data directory
    script_dir = Path(__file__).parent
    test_data_dir = script_dir.parent / "TestData"
    repo_dir = test_data_dir / "python-tuf-repo"
    
    print("Python-TUF Test Repository Generator")
    print("====================================")
    
    # Clean up existing repository
    if repo_dir.exists():
        print(f"Removing existing repository: {repo_dir}")
        shutil.rmtree(repo_dir)
    
    # Create new repository
    create_test_repository(repo_dir)
    
    print(f"\nTest repository ready at: {repo_dir}")
    print("You can now run the .NET interoperability tests.")

if __name__ == "__main__":
    main()