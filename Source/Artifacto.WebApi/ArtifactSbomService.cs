using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Models;

using CycloneDX;
using CycloneDX.Models;

using Microsoft.Extensions.Logging;

using OneOf;

using JsonSerializer = CycloneDX.Json.Serializer;
using JsonValidator = CycloneDX.Json.Validator;
using XmlSerializer = CycloneDX.Xml.Serializer;
using XmlValidator = CycloneDX.Xml.Validator;

namespace Artifacto.WebApi;

public interface IArtifactSbomService
{
    Task<OneOf<CanonicalArtifactSbom, BadRequestError>> NormalizeAsync(Stream uploadedStream, string artifactVersion, string? contentType, CancellationToken cancellationToken = default);

    Task<OneOf<GeneratedArtifactSbom, BadRequestError>> GenerateDownloadAsync(Stream canonicalJsonStream, string? format, string? specVersion, CancellationToken cancellationToken = default);
}

public record CanonicalArtifactSbom(byte[] Content, ulong FileSizeBytes, string SpecVersion);

public record GeneratedArtifactSbom(byte[] Content, string ContentType, string FileExtension);

internal enum ArtifactSbomDocumentFormat
{
    Json,
    Xml
}

public class ArtifactSbomService : IArtifactSbomService
{
    private readonly ILogger<ArtifactSbomService> _logger;

    public ArtifactSbomService(ILogger<ArtifactSbomService> logger)
    {
        _logger = logger;
    }

    public async Task<OneOf<CanonicalArtifactSbom, BadRequestError>> NormalizeAsync(Stream uploadedStream, string artifactVersion, string? contentType, CancellationToken cancellationToken = default)
    {
        byte[] uploadedContent;
        using (MemoryStream memoryStream = new())
        {
            await uploadedStream.CopyToAsync(memoryStream, cancellationToken);
            uploadedContent = memoryStream.ToArray();
        }

        if (uploadedContent.Length == 0)
        {
            return new BadRequestError("No SBOM uploaded or file is empty.");
        }

        if (!TryDetectFormat(contentType, uploadedContent, out ArtifactSbomDocumentFormat inputFormat))
        {
            return new BadRequestError("Unsupported SBOM format. Only CycloneDX JSON and XML are supported.");
        }

        Bom bom;
        try
        {
            bom = await DeserializeAsync(uploadedContent, inputFormat, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize uploaded SBOM for artifact version {ArtifactVersion}", artifactVersion);
            return new BadRequestError("Uploaded file is not a valid CycloneDX SBOM.");
        }

        if (!TryValidate(uploadedContent, inputFormat, bom.SpecVersion, out string validationMessage))
        {
            return new BadRequestError(validationMessage);
        }

        string? bomComponentVersion = bom.Metadata?.Component?.Version;
        if (string.IsNullOrWhiteSpace(bomComponentVersion))
        {
            return new BadRequestError("CycloneDX metadata.component.version is required.");
        }

        if (!string.Equals(bomComponentVersion, artifactVersion, StringComparison.Ordinal))
        {
            return new BadRequestError("CycloneDX metadata.component.version must match the artifact version.");
        }

        string canonicalJson;
        try
        {
            canonicalJson = JsonSerializer.Serialize(bom);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize canonical JSON SBOM for artifact version {ArtifactVersion}", artifactVersion);
            return new BadRequestError("Failed to convert the SBOM to canonical JSON.");
        }

        byte[] canonicalContent = Encoding.UTF8.GetBytes(canonicalJson);
        if (!TryValidate(canonicalContent, ArtifactSbomDocumentFormat.Json, bom.SpecVersion, out string canonicalValidationMessage))
        {
            return new BadRequestError(canonicalValidationMessage);
        }

        return new CanonicalArtifactSbom(canonicalContent, (ulong)canonicalContent.LongLength, bom.SpecVersionString);
    }

    public async Task<OneOf<GeneratedArtifactSbom, BadRequestError>> GenerateDownloadAsync(Stream canonicalJsonStream, string? format, string? specVersion, CancellationToken cancellationToken = default)
    {
        if (!TryParseOutputFormat(format, out ArtifactSbomDocumentFormat outputFormat, out string? formatErrorMessage))
        {
            return new BadRequestError(formatErrorMessage!);
        }

        if (!TryParseSpecificationVersion(specVersion, out SpecificationVersion specificationVersion, out string? specVersionErrorMessage))
        {
            return new BadRequestError(specVersionErrorMessage!);
        }

        Bom bom;
        try
        {
            bom = await JsonSerializer.DeserializeAsync(canonicalJsonStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize canonical stored SBOM.");
            return new BadRequestError("Stored SBOM is invalid and cannot be re-serialized.");
        }

        bom.SpecVersion = specificationVersion;

        string serializedContent;
        string contentType;
        string fileExtension;

        try
        {
            if (outputFormat == ArtifactSbomDocumentFormat.Xml)
            {
                serializedContent = XmlSerializer.Serialize(bom);
                contentType = "application/xml";
                fileExtension = "xml";
            }
            else
            {
                serializedContent = JsonSerializer.Serialize(bom);
                contentType = "application/json";
                fileExtension = "json";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize SBOM for download as {Format} {SpecVersion}", outputFormat, specificationVersion);
            return new BadRequestError("Failed to serialize the requested SBOM representation.");
        }

        byte[] serializedBytes = Encoding.UTF8.GetBytes(serializedContent);
        if (!TryValidate(serializedBytes, outputFormat, specificationVersion, out string validationMessage))
        {
            return new BadRequestError(validationMessage);
        }

        return new GeneratedArtifactSbom(serializedBytes, contentType, fileExtension);
    }


    private static async Task<Bom> DeserializeAsync(byte[] content, ArtifactSbomDocumentFormat format, CancellationToken cancellationToken)
    {
        using MemoryStream memoryStream = new(content, writable: false);
        if (format == ArtifactSbomDocumentFormat.Xml)
        {
            return XmlSerializer.Deserialize(memoryStream);
        }

        return await JsonSerializer.DeserializeAsync(memoryStream);
    }

    private static bool TryParseOutputFormat(string? format, out ArtifactSbomDocumentFormat outputFormat, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(format) || string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = ArtifactSbomDocumentFormat.Json;
            errorMessage = null;
            return true;
        }

        if (string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = ArtifactSbomDocumentFormat.Xml;
            errorMessage = null;
            return true;
        }

        outputFormat = ArtifactSbomDocumentFormat.Json;
        errorMessage = $"Unsupported SBOM format '{format}'.";
        return false;
    }

    private static bool TryDetectFormat(string? contentType, byte[] content, out ArtifactSbomDocumentFormat format)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                format = ArtifactSbomDocumentFormat.Xml;
                return true;
            }

            if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                format = ArtifactSbomDocumentFormat.Json;
                return true;
            }
        }

        for (int index = 0; index < content.Length; index++)
        {
            byte currentByte = content[index];
            if (currentByte == (byte)' ' || currentByte == (byte)'\t' || currentByte == (byte)'\r' || currentByte == (byte)'\n')
            {
                continue;
            }

            if (currentByte == (byte)'<')
            {
                format = ArtifactSbomDocumentFormat.Xml;
                return true;
            }

            if (currentByte == (byte)'{' || currentByte == (byte)'[')
            {
                format = ArtifactSbomDocumentFormat.Json;
                return true;
            }

            break;
        }

        format = ArtifactSbomDocumentFormat.Json;
        return false;
    }

    private static bool TryParseSpecificationVersion(string? specVersion, out SpecificationVersion specificationVersion, out string? errorMessage)
    {
        string normalizedSpecVersion = string.IsNullOrWhiteSpace(specVersion) ? "1.7" : specVersion;
        switch (normalizedSpecVersion)
        {
            case "1.3":
                specificationVersion = SpecificationVersion.v1_3;
                errorMessage = null;
                return true;
            case "1.4":
                specificationVersion = SpecificationVersion.v1_4;
                errorMessage = null;
                return true;
            case "1.5":
                specificationVersion = SpecificationVersion.v1_5;
                errorMessage = null;
                return true;
            case "1.6":
                specificationVersion = SpecificationVersion.v1_6;
                errorMessage = null;
                return true;
            case "1.7":
                specificationVersion = SpecificationVersion.v1_7;
                errorMessage = null;
                return true;
            default:
                specificationVersion = SpecificationVersion.v1_7;
                errorMessage = $"Unsupported CycloneDX specification version '{normalizedSpecVersion}'.";
                return false;
        }
    }

    private static bool TryValidate(byte[] content, ArtifactSbomDocumentFormat format, SpecificationVersion specificationVersion, out string errorMessage)
    {
        string serializedContent = Encoding.UTF8.GetString(content);
        object validationResult = format == ArtifactSbomDocumentFormat.Xml
            ? XmlValidator.Validate(serializedContent, specificationVersion)
            : JsonValidator.Validate(serializedContent, specificationVersion);

        PropertyInfo? validProperty = validationResult.GetType().GetProperty("Valid");
        if (validProperty?.GetValue(validationResult) is bool isValid && isValid)
        {
            errorMessage = string.Empty;
            return true;
        }

        List<string> validationMessages = [];
        PropertyInfo? messagesProperty = validationResult.GetType().GetProperty("Messages");
        if (messagesProperty?.GetValue(validationResult) is IEnumerable messages)
        {
            foreach (object? message in messages)
            {
                if (message is not null)
                {
                    validationMessages.Add(message.ToString() ?? string.Empty);
                }
            }
        }

        if (validationMessages.Count == 0)
        {
            errorMessage = $"CycloneDX {(format == ArtifactSbomDocumentFormat.Xml ? "XML" : "JSON")} validation failed.";
            return false;
        }

        errorMessage = string.Join(" ", validationMessages);
        return false;
    }
}