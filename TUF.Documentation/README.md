# TUF .NET Documentation

This project generates comprehensive API documentation and developer guides for TUF .NET using DocFX.

## Prerequisites

- .NET 8.0 or later
- DocFX CLI tool: `dotnet tool install -g docfx`

## Building Documentation

### Generate API Documentation
```bash
# From the TUF.Documentation directory
dotnet run

# Or run DocFX directly
docfx docfx.json
```

### Serve Documentation Locally
```bash
# Generate and serve with auto-reload
dotnet run -- --serve

# Or serve pre-built documentation
docfx serve _site
```

The documentation will be available at `http://localhost:8080`

## Project Structure

```
TUF.Documentation/
├── docfx.json              # DocFX configuration
├── index.md                # Main documentation homepage
├── toc.yml                 # Top-level table of contents
├── articles/               # Developer guides and tutorials
│   ├── getting-started.md  # Getting started guide
│   ├── migration/          # Migration guides from other TUF implementations
│   └── toc.yml            # Articles table of contents
├── api/                    # Generated API documentation (auto-created)
├── _site/                  # Generated documentation site (auto-created)
└── Program.cs             # Documentation generator utility
```

## Content Organization

### API Reference (`api/`)
Auto-generated from XML documentation comments in the source code:
- Complete API reference for all public types
- Method signatures, parameters, and return types  
- Code examples and usage notes
- Cross-references between related types

### Developer Guides (`articles/`)
Hand-crafted guides covering:
- **Getting Started** - Basic TUF .NET integration
- **Repository Management** - Creating and maintaining TUF repositories
- **Advanced Features** - Multi-repository, delegations, custom metadata
- **Security Best Practices** - Key management and production deployment
- **Migration Guides** - Moving from Python TUF, Go TUF, etc.
- **Troubleshooting** - Common issues and solutions

## Contributing Documentation

### Adding API Documentation
API documentation is generated from XML documentation comments in the source code:

```csharp
/// <summary>
/// Brief description of what this method does.
/// </summary>
/// <param name="parameter">Description of the parameter</param>
/// <returns>Description of the return value</returns>
/// <example>
/// <code>
/// var result = MyMethod("example");
/// Console.WriteLine($"Result: {result}");
/// </code>
/// </example>
public string MyMethod(string parameter)
{
    // Implementation
}
```

### Adding Developer Guides
1. Create a new `.md` file in the `articles/` directory
2. Add the article to `articles/toc.yml`
3. Follow the existing style and structure
4. Include code examples and practical guidance

### Documentation Style Guide
- Use clear, concise language
- Provide practical, working code examples
- Include security considerations where relevant
- Cross-reference related concepts and APIs
- Test all code examples for accuracy

## Deployment

The documentation can be deployed to any static hosting service:

### GitHub Pages
```yaml
# .github/workflows/docs.yml
- name: Generate documentation
  run: |
    cd TUF.Documentation
    dotnet run
    
- name: Deploy to GitHub Pages
  uses: peaceiris/actions-gh-pages@v3
  with:
    github_token: ${{ secrets.GITHUB_TOKEN }}
    publish_dir: ./TUF.Documentation/_site
```

### Azure Static Web Apps
```yaml
- name: Build documentation
  run: |
    cd TUF.Documentation  
    dotnet run
    
- name: Deploy to Azure Static Web Apps
  uses: Azure/static-web-apps-deploy@v1
  with:
    app_location: "./TUF.Documentation/_site"
```

## Customization

### Themes and Styling
DocFX uses customizable templates. To modify the appearance:

1. Create a custom template in `templates/`
2. Update `docfx.json` to reference the custom template
3. Customize CSS, JavaScript, and layout files

### Configuration
Key configuration options in `docfx.json`:
- `metadata`: Controls API extraction from source code
- `build.content`: Defines which files to include
- `globalMetadata`: Site-wide settings and variables
- `template`: Specifies the theme/template to use

For more details, see the [DocFX documentation](https://dotnet.github.io/docfx/).