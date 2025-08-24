# Development Guide

This guide contains information for developers working on the Artifacto project.

## 🛠️ Development Setup

### Prerequisites

- [Docker](https://www.docker.com/get-started)
- [Visual Studio Code](https://code.visualstudio.com/)

### Using Dev Container (Recommended)

1. Open the project in Visual Studio Code
2. Install the "Dev Containers" extension
3. Press `Ctrl+Shift+P` and select "Dev Containers: Reopen in Container"
4. The development environment will be automatically configured

## 🏗️ Architecture

Artifacto consists of four core components:

1. **Persistent File Storage** - File system-based storage for binary artifacts
2. **Database** - SQLite database for metadata storage using Entity Framework
3. **Web API** - ASP.NET RESTful API with OpenAPI specification
4. **Web Application** - Blazor Server-Side rendered web interface with MudBlazor UI

## 📁 Project Structure

```
Artifacto/
├── Source/
│   ├── Artifacto.sln                    # Main solution file
│   ├── Directory.Build.props            # Shared project properties
│   ├── Artifacto.WebApplication/        # Blazor web application
│   ├── Artifacto.WebApi/                # ASP.NET Web API
│   ├── Artifacto.Client/                # Generated API client
│   ├── Artifacto.Database/              # Entity Framework data layer
│   ├── Artifacto.FileStorage/           # File storage abstraction
│   ├── Artifacto.Models/                # Domain models
│   ├── Artifacto.Repository/            # Data access layer
│   ├── Artifacto.OpenApi/               # OpenAPI specification
├── Documentation/                       # Additional project documentation
├── Run-Solution.ps1                     # Quick start script
├── Build-Solution.ps1                   # Build script
└── Format-Solution.ps1                  # Code formatting script
```

## 🛡️ Code Generation

This project uses a specification-first approach with NSwag for API development:

- **OpenAPI Specification**: `Source/Artifacto.OpenApi/openapi.yaml`
- **Generated Files**:
  - `Source/Artifacto.Client/Clients.cs` - API client code
  - `Source/Artifacto.WebApi/Controllers.cs` - API controllers
  - `Source/Artifacto.OpenApi/openapi.json` - JSON OpenAPI spec

### Regenerating Code

To regenerate the API client and controller code after modifying the OpenAPI specification:

```powershell
cd Source/Artifacto.OpenApi
./Invoke-NSwag.ps1
```

⚠️ **Never modify generated files manually** - they will be overwritten during the next generation.

## 📝 Coding Standards

- Do not use `var` - always use explicit type names
- Use collection initializers instead of `.ToList()` or `.ToArray()` when possible
- Follow "Happy Path" coding strategies - handle errors and edge cases first, return early
- Implicit usings are disabled - use explicit using statements
- Nullable reference types are enabled

## 🔧 Technologies Used

- **.NET 9.0** - Core framework
- **ASP.NET Core** - Web API framework
- **Blazor Server** - Web application framework
- **MudBlazor** - UI component library
- **Entity Framework Core** - Data access
- **SQLite** - Database
- **NSwag** - OpenAPI code generation
- **Docker** - Containerization
- **PowerShell** - Scripting
- **NGINX** - Reverse proxy and static file serving

## 🧪 Testing

Currently, no testing framework is implemented. Testing standards will be established in future development phases.

## 🚀 Building and Running

### Development Mode

```powershell
./Run-Solution.ps1
```

## 🐛 Debugging

## 📦 Database Migrations

To create new database migrations:

```powershell
cd Source/Artifacto.Database
./New-Migrations.ps1 -MigrationName "YourMigrationName"
```

## 🔒 Security Considerations

Currently, no security considerations are implemented.