# Contributing to AspNetDebugDashboard

Thank you for your interest in contributing to AspNetDebugDashboard! We welcome contributions from the community.

## How to Contribute

### Reporting Issues

1. **Search existing issues** first to avoid duplicates
2. **Use the issue template** when creating new issues
3. **Provide detailed information** including:
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (.NET version, OS, etc.)
   - Sample code if applicable

### Contributing Code

1. **Fork the repository** and create a feature branch
2. **Follow the coding standards** (see below)
3. **Write tests** for new functionality
4. **Update documentation** as needed
5. **Submit a pull request** with a clear description

## Development Setup

### Prerequisites
- .NET 7.0 SDK or later
- Visual Studio 2022 or VS Code
- Git

### Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/eladser/AspNetDebugDashboard.git
   cd AspNetDebugDashboard
   ```

2. Restore packages:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

5. Run the sample application:
   ```bash
   cd samples/SampleApp
   dotnet run
   ```

6. Open the debug dashboard:
   ```
   https://localhost:5001/_debug
   ```

## Project Structure

```
AspNetDebugDashboard/
├── src/
│   └── AspNetDebugDashboard/        # Main NuGet package
│       ├── Core/                    # Core models and services
│       ├── Storage/                 # LiteDB storage implementation
│       ├── Middleware/              # ASP.NET Core middleware
│       ├── Interceptors/            # EF Core interceptors
│       ├── Web/                     # Dashboard controllers and views
│       └── Extensions/              # Service extensions
├── samples/
│   └── SampleApp/                   # Sample ASP.NET Core application
├── tests/
│   └── AspNetDebugDashboard.Tests/ # Unit tests
└── docs/                            # Documentation
```

## Coding Standards

### C# Code Style
- Follow Microsoft's C# coding conventions
- Use PascalCase for public members
- Use camelCase for private fields and parameters
- Use meaningful names for variables and methods
- Add XML documentation for public APIs
- Use `var` when the type is obvious
- Prefer `async/await` over `Task.Result`

### Example:
```csharp
/// <summary>
/// Stores a request entry in the database
/// </summary>
/// <param name="request">The request entry to store</param>
/// <returns>The ID of the stored request</returns>
public async Task<string> StoreRequestAsync(RequestEntry request)
{
    // Implementation
}
```

### Testing
- Write unit tests for all new functionality
- Use descriptive test method names
- Follow the Arrange-Act-Assert pattern
- Mock external dependencies
- Aim for high code coverage

### Documentation
- Update README.md for new features
- Add XML documentation for public APIs
- Update CHANGELOG.md for notable changes
- Include code examples where helpful

## Pull Request Process

1. **Create a feature branch** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** with clear, focused commits

3. **Write or update tests** to cover your changes

4. **Update documentation** if needed

5. **Test your changes** thoroughly:
   ```bash
   dotnet test
   ```

6. **Submit a pull request** with:
   - Clear title and description
   - Reference to related issues
   - Screenshots for UI changes
   - Breaking change notes if applicable

## Code Review Guidelines

### For Contributors
- Be responsive to feedback
- Keep pull requests focused and small
- Write clear commit messages
- Test your changes thoroughly

### For Reviewers
- Be constructive and respectful
- Focus on code quality and maintainability
- Test the changes locally when possible
- Provide specific suggestions for improvements

## Release Process

1. Update version numbers in project files
2. Update CHANGELOG.md with new features and fixes
3. Create a release branch and test thoroughly
4. Merge to main and tag the release
5. Publish to NuGet

## Getting Help

If you need help or have questions:

1. Check the existing documentation
2. Search through existing issues
3. Create a new issue with the "question" label
4. Join our discussions for general questions

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/0/code_of_conduct/). By participating, you are expected to uphold this code.

## License

By contributing to AspNetDebugDashboard, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing! 🚀
