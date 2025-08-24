# Project Overview

This project is a web application and web API that allows users to upload and retrieve single file versioned binary artifacts. Artifacts are organized into projects. It is built using ASP.NET and Blazor.

## Folder Structure

- `/Source`: Contains the source code for the project.
- `/Source/Artifacto.WebApplication`: Contains the source code for the frontend web application.
- `/Source/Artifacto.WebApi`: Contains the source code for the backend web API.
- `/Documentation`: Contains documentation for the project.

## Libraries, Languages, and Frameworks

- Blazor and MudBlazor for the frontend.
- ASP.NET and EntityFramework for the backend.
- NSwag for code generation.
- PowerShell for scripting and terminal commands.
- Docker for deployment.

## Coding Standards

- Do not use `var`. Always use the type name.
- Use collection initializers when possible instead of `.ToList()` or `.ToArray()`.
- Follow "Happy Path" coding strategies - handle errors and edge cases first, return early
- Whenever you inject logging, always put the ILogger as the first parameter of the constructor.

## Automatically Generated Code

Automatically generated code should never be modified manually.

This project generates 3 files:
- `/Source/Artifacto.Client/Clients.cs`: API client code.
- `/Source/Artifacto.WebApi/Controllers.cs`: API controllers.
- `/Source/Artifacto.OpenApi/openapi.json`: JSON version of the OpenAPI specification.

The files are generated using NSwag by running the `/Source/Artifacto.OpenApi/Invoke-NSwag.ps1` and are based on the OpenAPI specifications found in `/Source/Artifacto.OpenApi/openapi.yaml`.

## Testing Standards

- Do not write tests for this codebase.

## Running the Project

The project can be correctly built and run by using the `Run-Solution.ps1` script in the root of the workspace.

## Other

- When reading files, read them 500 lines at a time.
- Never automatically attempt to create a commit.
- Only create a commit if asked to directly.
- Whenever you run terminal commands. Assume you are already in a PowerShell 7 environment.