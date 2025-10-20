# TUF Security Auditor

A comprehensive security auditing tool for TUF repositories that validates repository structure, checks for security misconfigurations, and generates detailed security reports.

## Features

### Repository Structure Validation
- ✅ Verify proper TUF metadata organization and file presence
- ✅ Validate metadata format and schema compliance
- ✅ Check consistent snapshot configuration
- ✅ Ensure proper target file organization

### Security Configuration Auditing
- ✅ Analyze key management and rotation policies
- ✅ Validate signature threshold requirements
- ✅ Check metadata expiration policies
- ✅ Audit delegation configurations
- ✅ Identify weak cryptographic settings

### Metadata Consistency Checks
- ✅ Cross-validate metadata consistency across roles
- ✅ Verify signature integrity and key mappings
- ✅ Check metadata version sequences
- ✅ Validate hash algorithm usage

### Security Reporting
- ✅ Generate comprehensive security audit reports
- ✅ Provide actionable security recommendations
- ✅ Export results in multiple formats (JSON, text, HTML)
- ✅ Security scoring and risk assessment

## Usage

```bash
# Basic repository audit
dotnet run -- audit /path/to/tuf/repository

# Detailed audit with recommendations
dotnet run -- audit /path/to/tuf/repository --detailed --recommendations

# Generate JSON report
dotnet run -- audit /path/to/tuf/repository --output-format json --output audit-report.json

# Audit specific security aspects
dotnet run -- audit /path/to/tuf/repository --focus keys,thresholds,expiration

# Continuous integration mode (exit codes for automation)
dotnet run -- audit /path/to/tuf/repository --ci-mode --fail-on-warnings
```

## Security Checks

### Critical Security Issues (Score: 0-100)
- **Key Management**: Single points of failure, weak key types, missing rotation
- **Threshold Settings**: Insufficient signature requirements
- **Metadata Security**: Expired metadata, weak hash algorithms
- **Repository Structure**: Missing files, inconsistent snapshots

### Security Best Practices
- **NIST Guidelines**: Compliance with NIST SP 800-57 key management
- **TUF Specification**: Full TUF 1.0+ specification compliance
- **Crypto Agility**: Multiple hash algorithm support
- **Operational Security**: Proper key rotation and threshold configurations

## Example Output

```
TUF Security Auditor v1.0.0
============================

Repository: /path/to/tuf/repository
Audit Date: 2025-10-20T10:30:00Z
Security Score: 85/100 (Good)

CRITICAL ISSUES (2):
❌ Root key threshold is 1 (recommended: 2+)
❌ Timestamp metadata expires in 2 days

WARNINGS (3):
⚠️  Only SHA-256 hash algorithm used (consider adding SHA-512)
⚠️  No key rotation performed in 180+ days
⚠️  Large delegation tree depth (5 levels)

RECOMMENDATIONS:
✅ Increase root key threshold to 2 or more
✅ Extend timestamp metadata expiration
✅ Add additional hash algorithms for crypto agility
✅ Implement regular key rotation policy

Repository Structure: ✅ Valid
Metadata Consistency: ✅ Valid
Key Management: ⚠️  Needs attention
Crypto Settings: ⚠️  Could be improved
```

## Integration

This tool can be integrated into:
- **CI/CD Pipelines**: Automated security validation
- **Security Monitoring**: Regular repository health checks
- **Compliance Audits**: Generate reports for security compliance
- **Development Workflows**: Pre-deployment security validation

## Relationship to TUF Ecosystem

This auditing tool helps achieve parity with mature TUF implementations by providing:
- **Python-TUF equivalent**: Security validation capabilities matching reference implementation
- **Go-TUF parity**: Repository management and validation tooling
- **Enterprise readiness**: Production-grade security auditing for enterprise TUF deployments
- **Developer experience**: Clear security feedback for TUF repository maintainers