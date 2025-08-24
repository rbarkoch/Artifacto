using System.Text;
using System.Text.RegularExpressions;

namespace Artifacto.Models;

/// <summary>
/// Represents a semantic version with support for major, minor, build, revision, and prerelease components.
/// Follows a pattern similar to semantic versioning with optional components.
/// </summary>
public record struct Version : IComparable, IComparable<Version>
{
    private const string PreviewPattern = @"^[a-z0-9]+$";
    
    /// <summary>
    /// Regular expression pattern that defines valid version strings.
    /// Supports major.minor.build.revision-prerelease format with optional components.
    /// </summary>
    public const string VersionPattern = @"^(?<major>[0-9]+)(\.(?<minor>[0-9]+))?(\.(?<build>[0-9]+))?(\.(?<revision>[0-9]+))?(-(?<prerelease>[a-z0-9]+))?$";

    /// <summary>
    /// Gets the major version component.
    /// </summary>
    public int Major { get; }
    
    /// <summary>
    /// Gets the minor version component, if specified.
    /// </summary>
    public int? Minor { get; }
    
    /// <summary>
    /// Gets the build version component, if specified.
    /// </summary>
    public int? Build { get; }
    
    /// <summary>
    /// Gets the revision version component, if specified.
    /// </summary>
    public int? Revision { get; }
    
    /// <summary>
    /// Gets the prerelease identifier, if specified.
    /// </summary>
    public string? PreRelease { get; }

    /// <summary>
    /// Gets a value indicating whether this version is a prerelease version.
    /// </summary>
    public readonly bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>
    /// Initializes a new instance of the <see cref="Version"/> struct.
    /// </summary>
    /// <param name="major">The major version component.</param>
    /// <param name="minor">The optional minor version component.</param>
    /// <param name="build">The optional build version component.</param>
    /// <param name="revision">The optional revision version component.</param>
    /// <param name="preview">The optional prerelease identifier.</param>
    /// <exception cref="FormatException">Thrown when the version components are invalid.</exception>
    public Version(int major, int? minor, int? build, int? revision, string? preview)
    {
        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
        PreRelease = preview;

        ThrowIfNotValid();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Version"/> struct by copying from another version.
    /// </summary>
    /// <param name="version">The version to copy from.</param>
    /// <exception cref="FormatException">Thrown when the version components are invalid.</exception>
    public Version(Version version)
    {
        Major = version.Major;
        Minor = version.Minor;
        Build = version.Build;
        Revision = version.Revision;
        PreRelease = version.PreRelease;

        ThrowIfNotValid();
    }

    /// <summary>
    /// Parses a version string into a <see cref="Version"/> instance.
    /// </summary>
    /// <param name="input">The version string to parse.</param>
    /// <returns>A <see cref="Version"/> instance representing the parsed version.</returns>
    /// <exception cref="FormatException">Thrown when the input string is not a valid version format.</exception>
    public static Version Parse(string input)
    {
        Match match = Regex.Match(input, VersionPattern);
        if (!match.Success)
        {
            throw new FormatException("Invalid version format.");
        }

        int major = int.Parse(match.Groups["major"].Value);
        int? minor = int.TryParse(match.Groups["minor"].Value, out int m) ? m : null;
        int? build = int.TryParse(match.Groups["build"].Value, out int b) ? b : null;
        int? revision = int.TryParse(match.Groups["revision"].Value, out int r) ? r : null;
        string? prerelease = match.Groups["prerelease"].Value;

        return new Version(major, minor, build, revision, prerelease);
    }

    /// <summary>
    /// Tries to parse a version string into a <see cref="Version"/> instance.
    /// </summary>
    /// <param name="input">The version string to parse.</param>
    /// <param name="version">When successful, contains the parsed version; otherwise, contains the default value.</param>
    /// <returns><c>true</c> if the parsing was successful; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string input, out Version version)
    {
        Match match = Regex.Match(input, VersionPattern);
        if (!match.Success)
        {
            version = new Version();
            return false;
        }

        int major = int.Parse(match.Groups["major"].Value);
        int? minor = int.TryParse(match.Groups["minor"].Value, out int m) ? m : null;
        int? build = int.TryParse(match.Groups["build"].Value, out int b) ? b : null;
        int? revision = int.TryParse(match.Groups["revision"].Value, out int r) ? r : null;
        string? prerelease = match.Groups["prerelease"].Value;

        version = new Version(major, minor, build, revision, prerelease);
        return true;
    }

    /// <summary>
    /// Validates the version components and throws an exception if any are invalid.
    /// </summary>
    /// <exception cref="FormatException">Thrown when version components are invalid.</exception>
    private readonly void ThrowIfNotValid()
    {
        if (Major < 0 || (Minor.HasValue && Minor < 0) || (Build.HasValue && Build < 0) || (Revision.HasValue && Revision < 0))
        {
            throw new FormatException("Version numbers must be non-negative.");
        }

        if (!Minor.HasValue && Build.HasValue)
        {
            throw new FormatException("Build number cannot be specified without a minor version.");
        }

        if (!Minor.HasValue && Revision.HasValue)
        {
            throw new FormatException("Revision number cannot be specified without a minor version.");
        }

        if (Minor.HasValue && !Build.HasValue && Revision.HasValue)
        {
            throw new FormatException("Revision number cannot be specified without a build number.");
        }

        if (!string.IsNullOrEmpty(PreRelease) && !Regex.IsMatch(PreRelease, PreviewPattern))
        {
            throw new FormatException("Preview identifier must be alphanumeric and lowercase.");
        }
    }

    /// <summary>
    /// Returns a string representation of this version.
    /// </summary>
    /// <returns>A string in the format "major[.minor[.build[.revision]]][-prerelease]".</returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append(Major.ToString());

        if (Minor.HasValue)
        {
            sb.Append($".{Minor.Value}");
        }

        if (Build.HasValue)
        {
            sb.Append($".{Build.Value}");
        }

        if (Revision.HasValue)
        {
            sb.Append($".{Revision.Value}");
        }

        if (!string.IsNullOrEmpty(PreRelease))
        {
            sb.Append($"-{PreRelease}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Compares this version to another version.
    /// </summary>
    /// <param name="other">The version to compare to.</param>
    /// <returns>A negative value if this version is less than <paramref name="other"/>, zero if they are equal, or a positive value if this version is greater.</returns>
    public int CompareTo(Version other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = CompareVersionPart(Minor, other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = CompareVersionPart(Build, other.Build);
        if (result != 0)
        {
            return result;
        }

        result = CompareVersionPart(Revision, other.Revision);
        if (result != 0)
        {
            return result;
        }

        // Pre-release comparison: empty prerelease (stable) > prerelease
        // This follows semantic versioning precedence where stable versions are greater than prereleases
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
        {
            return 1; // This version (stable) is greater than other (prerelease)
        }
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
        {
            return -1; // This version (prerelease) is less than other (stable)
        }

        return string.Compare(PreRelease, other.PreRelease, StringComparison.InvariantCulture);
    }

    /// <summary>
    /// Compares this version to another object.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>A negative value if this version is less than <paramref name="obj"/>, zero if they are equal, or a positive value if this version is greater.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="Version"/>.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is not Version other)
        {
            throw new ArgumentException("Object is not a Version");
        }

        return CompareTo(other);
    }

    /// <summary>
    /// Compares two optional version parts.
    /// </summary>
    /// <param name="part1">The first version part to compare.</param>
    /// <param name="part2">The second version part to compare.</param>
    /// <returns>A negative value if <paramref name="part1"/> is less than <paramref name="part2"/>, zero if they are equal, or a positive value if <paramref name="part1"/> is greater.</returns>
    private readonly int CompareVersionPart(int? part1, int? part2)
    {
        if (part1.HasValue && part2.HasValue)
        {
            return part1.Value.CompareTo(part2.Value);
        }
        if (part1.HasValue)
        {
            return 1; // part1 is greater
        }
        if (part2.HasValue)
        {
            return -1; // part2 is greater
        }
        return 0; // both are null
    }
}
