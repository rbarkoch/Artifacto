using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Artifacto.FileStorage;

/// <summary>
/// Provides utility methods for calculating file hashes.
/// </summary>
public static class HashCalculator
{
    /// <summary>
    /// Calculates the SHA256 hash of a stream while copying it to another stream.
    /// This allows hash calculation during file save operations without requiring multiple reads.
    /// </summary>
    /// <param name="inputStream">The input stream to read from and calculate hash for.</param>
    /// <param name="outputStream">The output stream to write the data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA256 hash as a hexadecimal string.</returns>
    public static async Task<string> CalculateSha256HashAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        using SHA256 sha256 = SHA256.Create();
        using CryptoStream cryptoStream = new(outputStream, sha256, CryptoStreamMode.Write, leaveOpen: true);
        
        await inputStream.CopyToAsync(cryptoStream, cancellationToken);
        await cryptoStream.FlushFinalBlockAsync(cancellationToken);
        
        byte[] hashBytes = sha256.Hash ?? throw new InvalidOperationException("Hash calculation failed");
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates the SHA256 hash of a stream without modifying the stream position.
    /// </summary>
    /// <param name="stream">The stream to calculate hash for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA256 hash as a hexadecimal string.</returns>
    public static async Task<string> CalculateSha256HashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
