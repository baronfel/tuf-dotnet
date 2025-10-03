package main

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os"
	"path/filepath"
)

// Basic TUF metadata structures for validation
type Signature struct {
	KeyID string `json:"keyid"`
	Sig   string `json:"sig"`
}

type RoleKeys struct {
	KeyIDs    []string `json:"keyids"`
	Threshold int      `json:"threshold"`
}

type Roles struct {
	Root      RoleKeys `json:"root"`
	Timestamp RoleKeys `json:"timestamp"`
	Snapshot  RoleKeys `json:"snapshot"`
	Targets   RoleKeys `json:"targets"`
}

type Key struct {
	KeyType string `json:"keytype"`
	Scheme  string `json:"scheme"`
	KeyVal  struct {
		Public string `json:"public"`
	} `json:"keyval"`
}

type RootSigned struct {
	Type               string            `json:"_type"`
	Version            int               `json:"version"`
	SpecVersion        string            `json:"spec_version"`
	Expires            string            `json:"expires"`
	ConsistentSnapshot bool              `json:"consistent_snapshot"`
	Keys               map[string]Key    `json:"keys"`
	Roles              Roles             `json:"roles"`
}

type RootMetadata struct {
	Signed     RootSigned  `json:"signed"`
	Signatures []Signature `json:"signatures"`
}

func testGoCanReadDotNetRepository(repoPath string) error {
	fmt.Printf("Testing Go TUF with .NET repository: %s\n", repoPath)

	// Check repository structure
	metadataDir := filepath.Join(repoPath, "metadata")
	targetsDir := filepath.Join(repoPath, "targets")

	if _, err := os.Stat(metadataDir); os.IsNotExist(err) {
		return fmt.Errorf("❌ Metadata directory not found: %s", metadataDir)
	}

	if _, err := os.Stat(targetsDir); os.IsNotExist(err) {
		return fmt.Errorf("❌ Targets directory not found: %s", targetsDir)
	}

	// Check for root.json
	rootPath := filepath.Join(metadataDir, "root.json")
	if _, err := os.Stat(rootPath); os.IsNotExist(err) {
		return fmt.Errorf("❌ Root metadata not found: %s", rootPath)
	}

	// Load and parse root metadata
	rootData, err := ioutil.ReadFile(rootPath)
	if err != nil {
		return fmt.Errorf("❌ Failed to read root metadata: %v", err)
	}

	fmt.Printf("✅ Successfully loaded root.json\n")
	fmt.Printf("   - File size: %d bytes\n", len(rootData))

	var rootMetadata RootMetadata
	if err := json.Unmarshal(rootData, &rootMetadata); err != nil {
		return fmt.Errorf("❌ Failed to parse root metadata JSON: %v", err)
	}

	fmt.Printf("✅ Successfully parsed root metadata using Go JSON\n")

	// Verify basic structure
	signed := rootMetadata.Signed
	signatures := rootMetadata.Signatures

	fmt.Printf("   - Type: %s\n", signed.Type)
	fmt.Printf("   - Version: %d\n", signed.Version)
	fmt.Printf("   - Spec version: %s\n", signed.SpecVersion)
	fmt.Printf("   - Keys: %d\n", len(signed.Keys))
	fmt.Printf("   - Signatures: %d\n", len(signatures))

	// Verify it's a root metadata
	if signed.Type != "root" {
		return fmt.Errorf("❌ Expected root metadata, got: %s", signed.Type)
	}

	// Verify required fields
	if signed.Version <= 0 {
		return fmt.Errorf("❌ Invalid version: %d", signed.Version)
	}

	if len(signed.Keys) == 0 {
		return fmt.Errorf("❌ No keys found in root metadata")
	}

	if len(signatures) == 0 {
		return fmt.Errorf("❌ No signatures found in root metadata")
	}

	// Verify roles structure
	if len(signed.Roles.Root.KeyIDs) == 0 {
		return fmt.Errorf("❌ No root key IDs found")
	}

	if signed.Roles.Root.Threshold <= 0 {
		return fmt.Errorf("❌ Invalid root threshold: %d", signed.Roles.Root.Threshold)
	}

	fmt.Printf("   - Expires: %s\n", signed.Expires)
	fmt.Printf("   - Consistent snapshot: %t\n", signed.ConsistentSnapshot)

	// List other metadata files
	metadataFiles, err := filepath.Glob(filepath.Join(metadataDir, "*.json"))
	if err != nil {
		return fmt.Errorf("❌ Failed to list metadata files: %v", err)
	}

	fmt.Printf("✅ Found %d metadata files:\n", len(metadataFiles))
	for _, file := range metadataFiles {
		info, err := os.Stat(file)
		if err == nil {
			fmt.Printf("   - %s (%d bytes)\n", filepath.Base(file), info.Size())
		}
	}

	// List target files
	targetFiles, err := filepath.Glob(filepath.Join(targetsDir, "*"))
	if err != nil {
		return fmt.Errorf("❌ Failed to list target files: %v", err)
	}

	fmt.Printf("✅ Found %d target files:\n", len(targetFiles))
	for _, file := range targetFiles {
		info, err := os.Stat(file)
		if err == nil && !info.IsDir() {
			fmt.Printf("   - %s (%d bytes)\n", filepath.Base(file), info.Size())
		}
	}

	fmt.Printf("✅ Go successfully validated .NET repository structure\n")
	return nil
}

func main() {
	if len(os.Args) != 2 {
		fmt.Fprintf(os.Stderr, "Usage: %s <repository_path>\n", os.Args[0])
		os.Exit(1)
	}

	repoPath := os.Args[1]

	if _, err := os.Stat(repoPath); os.IsNotExist(err) {
		fmt.Fprintf(os.Stderr, "❌ Repository path does not exist: %s\n", repoPath)
		os.Exit(1)
	}

	if err := testGoCanReadDotNetRepository(repoPath); err != nil {
		fmt.Fprintf(os.Stderr, "💥 FAILURE: %v\n", err)
		os.Exit(1)
	}

	fmt.Println("🎉 SUCCESS: Go can read .NET TUF repository")
}