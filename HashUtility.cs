using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Cee3;

/// <summary>
/// Utility class for calculating file hashes and comparing with S3 ETags
/// </summary>
public static class HashUtility
{
    /// <summary>
    /// Calculates MD5 hash of a file, matching S3 ETag format for simple uploads
    /// </summary>
    public static async Task<string> CalculateMD5Async(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var hash = await md5.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Calculates MD5 hash synchronously
    /// </summary>
    public static string CalculateMD5(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Calculates MD5 hash with progress reporting for large files
    /// </summary>
    public static async Task<string> CalculateMD5WithProgressAsync(
        string filePath,
        IProgress<long>? progress = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileInfo = new FileInfo(filePath);
        long totalBytesRead = 0;
        const int bufferSize = 81920; // 80KB buffer

        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }

            md5.TransformFinalBlock(buffer, 0, 0);
            var hash = md5.Hash!;
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Compares local file MD5 with S3 ETag
    /// Returns true if they match (indicating duplicate content)
    /// </summary>
    public static bool IsETagMatch(string localMD5, string s3ETag)
    {
        // Remove quotes from S3 ETag if present
        var cleanETag = s3ETag.Trim('"');

        // Check if ETag indicates multipart upload (contains dash)
        if (cleanETag.Contains("-"))
        {
            // For multipart uploads, we can't directly compare
            // The ETag format is: md5-of-md5s-partcount
            return false; // Conservative: treat as not matching
        }

        // For simple uploads, ETag is the MD5 hash
        return string.Equals(localMD5, cleanETag, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if an ETag indicates a multipart upload
    /// </summary>
    public static bool IsMultipartETag(string etag)
    {
        var cleanETag = etag.Trim('"');
        return cleanETag.Contains("-");
    }

    /// <summary>
    /// Formats a byte array hash to hex string matching S3 format
    /// </summary>
    public static string FormatHashToHex(byte[] hash)
    {
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Displays hash calculation progress
    /// </summary>
    public static void DisplayHashProgress(long bytesProcessed, long totalBytes)
    {
        var percentage = (double)bytesProcessed / totalBytes * 100;
        var mbProcessed = bytesProcessed / (1024.0 * 1024.0);
        var mbTotal = totalBytes / (1024.0 * 1024.0);

        Console.Write($"\r  Calculating hash: {percentage:F1}% ({mbProcessed:F1}/{mbTotal:F1} MB)");

        if (bytesProcessed >= totalBytes)
        {
            Console.WriteLine(); // New line when complete
        }
    }
}
