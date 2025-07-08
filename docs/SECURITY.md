# Security Policy

## Supported Versions

We release patches for security vulnerabilities for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability, please report it to us responsibly.

### How to Report

1. **Do not** create a public GitHub issue for security vulnerabilities
2. Email us at [security@aspnetdebugdashboard.com] or create a private security advisory
3. Include as much information as possible:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### What to Expect

- We will acknowledge your report within 48 hours
- We will provide a detailed response within 7 days
- We will keep you informed of our progress
- We will credit you in our security advisory (unless you prefer to remain anonymous)

## Security Considerations

### Development Environment Only

**Important**: AspNetDebugDashboard is designed for development environments only. By default, it will not run in production environments.

### Data Security

- The dashboard may capture sensitive data (request bodies, headers, SQL queries)
- Always review the configuration options to exclude sensitive information
- Use appropriate exclusion lists for headers and paths
- Consider the security implications of logging request/response bodies

### Network Security

- The dashboard is accessible via HTTP/HTTPS on localhost
- No authentication is required by default
- Ensure your development environment is properly secured

### Configuration Security

- Review all configuration options before deployment
- Use environment-specific configurations
- Never enable in production without proper security measures

## Security Best Practices

### For Users

1. **Environment Isolation**: Only use in development environments
2. **Data Exclusion**: Configure exclusions for sensitive data
3. **Network Access**: Restrict network access to development machines
4. **Regular Updates**: Keep the package updated to the latest version

### For Contributors

1. **Code Review**: All code changes must be reviewed
2. **Security Testing**: Test for common security vulnerabilities
3. **Dependencies**: Keep dependencies updated and secure
4. **Documentation**: Document security considerations for new features

## Common Security Scenarios

### Sensitive Data Exposure

```csharp
// Good: Exclude sensitive headers
builder.Services.AddDebugDashboard(config =>
{
    config.ExcludedHeaders = new List<string> 
    { 
        "Authorization", 
        "Cookie", 
        "X-API-Key",
        "X-Auth-Token"
    };
});
```

### Path Exclusion

```csharp
// Good: Exclude sensitive endpoints
builder.Services.AddDebugDashboard(config =>
{
    config.ExcludedPaths = new List<string>
    {
        "/_debug",
        "/admin",
        "/api/auth",
        "/api/payments"
    };
});
```

### Body Logging

```csharp
// Good: Disable body logging for sensitive data
builder.Services.AddDebugDashboard(config =>
{
    config.LogRequestBodies = false;  // Disable if handling sensitive data
    config.LogResponseBodies = false; // Disable if returning sensitive data
});
```

## Vulnerability Disclosure Timeline

1. **Day 0**: Vulnerability reported
2. **Day 1-2**: Acknowledgment sent to reporter
3. **Day 3-7**: Initial assessment and response
4. **Day 8-30**: Development of fix
5. **Day 31-45**: Testing and validation
6. **Day 46-60**: Release and public disclosure

## Security Updates

Security updates will be released as patch versions and will be clearly marked in the changelog. We recommend updating immediately when security patches are available.

## Contact

For security-related questions or concerns, please contact us at [security@aspnetdebugdashboard.com].
