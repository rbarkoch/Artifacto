namespace Artifacto.Models;

/// <summary>
/// Represents an error indicating that a requested resource was not found.
/// </summary>
/// <param name="Message">The error message describing what was not found.</param>
public record struct NotFoundError(string Message);

/// <summary>
/// Represents an error indicating that a request was invalid or malformed.
/// </summary>
/// <param name="Message">The error message describing why the request was invalid.</param>
public record struct BadRequestError(string Message);

/// <summary>
/// Represents an error indicating that a request conflicts with the current state of the resource.
/// </summary>
/// <param name="Message">The error message describing the conflict.</param>
public record struct ConflictError(string Message);

