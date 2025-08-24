# Software Architectural Design

This document describes the architectural design for a software solution called
"Artifacto".

## 1. Description
Artifacto is a solution for storing binary artifacts. Artifacts are a single
versioned binary of a project. The goal of the tool is to provide an easy way
for software teams to store the final artifacts of a their software product.
Typically the final artifact will be an installer or zipped bundle of files.

## 2. Software Architecture

### 2.1 Overall Architecture

Artifacto is composed of four core components:

1. A persistent file storage solution for storing the raw binary artifacts.
2. A database for storing metadata about the currently stored binary artifacts.
3. A RESTful HTTP web API used for querying and storing artifacts.
4. A web application for user-friendly interaction of the system through a web
   browser.

The web application interacts with the underlying system via the web API. The
web API coordinates the rest of the interactions between the database and
persistent file storage.

### 2.2 Models

The system contains and manages several simple models. Every domain should have
its own variants of these models and should be converted between each other
using C# extension methods in order to maintain separation of concerns.

**Projects** Projects are used to group all versions of a single collection of
artifacts. For example, all the various installers for a single software
installer.

**Artifact** A single versioned binary within a project.

Examples: Project: MyWebBrowser Artifact: V1.0.2

### 2.2.1 Project

Projects contain the following information.

| Name | Type | Description |
| :-- | :--: | :-- |
| ProjectId | int | A unique integer identifier for the project. |
| Key | String | A unique string identifier which contains only lowercase letters, numbers, and dashes which is used to identify the project. Must follow the pattern `^[a-z0-9]+(-?[a-z0-9]+)*$`. |
| Name | String | An optional human readable display name for the project. |
| Description | String | An optional human readable description about the project. |
| ArtifactCount | int | The number of artifacts currently stored in this project. |
| LatestStableVersion | String | The latest stable (non-prerelease) version of artifacts in this project. |
| LatestVersion | String | The latest version (including prerelease) of artifacts in this project. |
| LatestStableVersionUploadDate | DateTime | The upload date of the latest stable version, if available. |
| LatestVersionUploadDate | DateTime | The upload date of the latest version, if available. |

### 2.2.2 Artifact

| Name | Type | Description |
| :-- | :--: | :-- |
| ProjectId | int | The unique integer identifier of the project that contains this artifact. |
| ArtifactId | int | A unique integer identifier for the artifact. |
| Version | Version | A semantic version object with support for major, minor, build, revision, and prerelease components. Follows the pattern `^(?<major>[0-9]+)(\.(?<minor>[0-9]+))?(\.(?<build>[0-9]+))?(\.(?<revision>[0-9]+))?(-(?<prerelease>[a-z0-9]+))?$`. |
| FileName | String | The original filename of the artifact. |
| FileSizeBytes | ulong | The size of the artifact file in bytes. |
| Sha256Hash | String | The SHA256 hash of the artifact file for integrity verification. |
| Timestamp | DateTime | The time when the artifact was uploaded or last modified. |
| Retained | Boolean | A flag indicating that this artifact should never be deleted automatically. |
| Locked | Boolean | A flag indicating that this artifact can never be reuploaded or modified. |

### 2.3 Technologies

This section describes which technologies should be used for each component of
the software.

### 2.3.1 Core Components

**Persistent File Store**

The persistent files store should use the regular file system by default. This
is easily usable, can be mounted via Docker volumes and easy to backup.

**Database**

The database should be a simple SQLite database which is easy to construct,
deploy, and backup. All database operations should be done through
EntityFramework which allows for easy management, migrations, and swapping of
database solutions.

**Web API**

The web API should use ASP.NET with controllers. The controllers should be
generated from an OpenAPI specification using a tool like NSwag. Developers are
expected to maintain the OpenAPI spec first, and then generate the necessary
code from the specification.

**Web Application** 

The web application should use Blazor with Server-Side rendering. This makes 
security easy to manage since keys can be stored on server-side. The web 
application uses MudBlazor for UI components and supports large file uploads 
with optimized streaming configurations.

All core components should interact with each other only through dependency
injected interfaces. Each domain should have it's own models which have the
properties necessary to interact within their own domain as if they were
developed entirely independently. Domain models should be convertible to the
core models which can be passed around and manipulated independently.

The project includes the following additional architectural components:

**Repository Layer** (`Artifacto.Repository`)

Provides data access abstraction over the Entity Framework DbContext, implementing
repository patterns for Projects and Artifacts with proper separation of concerns.

**File Storage Layer** (`Artifacto.FileStorage`) 

Implements file system-based storage for binary artifacts with support for project
organization, integrity verification via SHA256 hashing, and secure path handling
to prevent directory traversal attacks.

**Generated API Client** (`Artifacto.Client`)

Contains automatically generated client code based on the OpenAPI specification,
providing strongly-typed access to the Web API from the Blazor application.

### 2.3.2 Development Setup

The entire project should be contained in a single git repository. All core
components should be contained in separate projects within a single solution.
Developers are expected to work with the project in Visual Studio Code which
should contain extensions for working with C#, git, and Docker.

Developers should work within a Dev Container with the Docker-in-Docker which
allows for the entire development environment to be self-contained and
distributed with the source code. The Dev-Container should include all the
necessary dependencies to build, run, and test the software. The dev container
should be Linux based, but should use PowerShell as the default shell for the
environment.

The project follows the following folder structure:

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

### 2.3.4 Other Tools and Technologies

**.NET/C#** 

The project uses .NET 9.0 and the latest C# language features. Common project
properties are centrally managed within a `Directory.Build.props` file.

**MudBlazor**

The web application uses MudBlazor as the UI component library for Blazor components,
providing Material Design components.

**.NET Host** 

The web API and web application utilize the .NET Host with standardized interfaces 
for logging and configuration.

**Logging** 

The system uses Serilog as the logging provider, accessed through the standard 
Microsoft.Extensions.Logging interfaces.

**Dependency Injection** 

All components interact through well-defined interfaces using dependency injection.

**Docker** 

The solution utilizes Docker for development, building, and deployment with 
Docker Compose support.

**NSwag** 

The API follows a Specification-First approach using NSwag for code generation.
The OpenAPI specification drives the generation of:
- API client code (`/Source/Artifacto.Client/Clients.cs`)
- API controller interfaces (`/Source/Artifacto.WebApi/Controllers.cs`) 
- JSON specification (`/Source/Artifacto.OpenApi/openapi.json`)

Generated files are automatically created from the OpenAPI specification and
should never be modified manually.

**Entity Framework Core**

The database layer uses Entity Framework Core with SQLite for data persistence
and schema migrations.

### 2.4 Code Standards

The project follows established coding standards defined within the `.editorconfig`
file. Source code formatting is maintained through automated tooling.

Key coding standards include:
- Explicit type declarations - `var` is not used
- Collection initializers are preferred over `.ToList()` or `.ToArray()` conversions
- "Happy Path" coding patterns - early returns for error and edge case handling
- Explicit using statements - implicit usings are disabled
- Nullable reference types are enabled project-wide
- Constructor parameter ordering places ILogger instances first when dependency injection is used


### 2.5 Error Handling

The web API should return appropriate HTTP status codes on error and should
include an error object which a description of why the error occurred if it was
not exceptional.

Special care should be taken to ensure consistency between the database and
persistence layers to avoid desynchronization of stored data.

### 2.7 Testing Standards

Currently, no testing framework is implemented. Testing standards will be 
established in future development phases as per the project roadmap.

### 2.8 Security

No security considerations are provided at this point and will be handled at a
later time.