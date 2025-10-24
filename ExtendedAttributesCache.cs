using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Cee3;

/// <summary>
/// Manages caching of S3 ETags in file extended attributes (NTFS ADS on Windows, xattr on Linux/Mac)
/// This allows fast duplicate detection without querying S3
/// </summary>
public static class ExtendedAttributesCache
{
    private const string ETAG_ATTRIBUTE_NAME = "cee3.s3.etag";
    private const string BUCKET_ATTRIBUTE_NAME = "cee3.s3.bucket";
    private const string KEY_ATTRIBUTE_NAME = "cee3.s3.key";
    private const string UPLOAD_DATE_ATTRIBUTE_NAME = "cee3.s3.uploaded";

    /// <summary>
    /// Cached S3 information for a file
    /// </summary>
    public class S3CacheInfo
    {
        public string? ETag { get; set; }
        public string? BucketName { get; set; }
        public string? Key { get; set; }
        public DateTime? UploadDate { get; set; }
    }

    /// <summary>
    /// Stores S3 ETag and metadata in file's extended attributes
    /// </summary>
    public static bool CacheETag(string filePath, string etag, string bucketName, string key)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CacheETagWindows(filePath, etag, bucketName, key);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return CacheETagUnix(filePath, etag, bucketName, key);
            }
            else
            {
                Console.WriteLine("  Note: Extended attributes not supported on this platform");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Could not cache ETag: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retrieves cached S3 information from file's extended attributes
    /// </summary>
    public static S3CacheInfo? GetCachedInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCachedInfoWindows(filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCachedInfoUnix(filePath);
            }
            else
            {
                return null;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Clears cached S3 information from file's extended attributes
    /// </summary>
    public static bool ClearCache(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ClearCacheWindows(filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ClearCacheUnix(filePath);
            }
            else
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if cached ETag matches current S3 ETag (fast local check)
    /// </summary>
    public static (bool hasCache, bool matches, string? cachedETag) CheckCachedETag(
        string filePath,
        string s3ETag)
    {
        var cached = GetCachedInfo(filePath);

        if (cached?.ETag == null)
        {
            return (false, false, null);
        }

        // Compare ETags (case-insensitive, strip quotes)
        var cachedETag = cached.ETag.Trim('"');
        var s3ETagClean = s3ETag.Trim('"');

        bool matches = string.Equals(cachedETag, s3ETagClean, StringComparison.OrdinalIgnoreCase);

        return (true, matches, cachedETag);
    }

    // ===== Windows Implementation (NTFS Alternate Data Streams) =====

    private static bool CacheETagWindows(string filePath, string etag, string bucketName, string key)
    {
        // Use NTFS Alternate Data Streams to store metadata
        WriteADS(filePath, ETAG_ATTRIBUTE_NAME, etag);
        WriteADS(filePath, BUCKET_ATTRIBUTE_NAME, bucketName);
        WriteADS(filePath, KEY_ATTRIBUTE_NAME, key);
        WriteADS(filePath, UPLOAD_DATE_ATTRIBUTE_NAME, DateTime.UtcNow.ToString("o"));
        return true;
    }

    private static S3CacheInfo? GetCachedInfoWindows(string filePath)
    {
        var etag = ReadADS(filePath, ETAG_ATTRIBUTE_NAME);
        if (string.IsNullOrEmpty(etag))
        {
            return null;
        }

        var bucket = ReadADS(filePath, BUCKET_ATTRIBUTE_NAME);
        var key = ReadADS(filePath, KEY_ATTRIBUTE_NAME);
        var uploadDateStr = ReadADS(filePath, UPLOAD_DATE_ATTRIBUTE_NAME);

        DateTime? uploadDate = null;
        if (DateTime.TryParse(uploadDateStr, out var parsed))
        {
            uploadDate = parsed;
        }

        return new S3CacheInfo
        {
            ETag = etag,
            BucketName = bucket,
            Key = key,
            UploadDate = uploadDate
        };
    }

    private static bool ClearCacheWindows(string filePath)
    {
        DeleteADS(filePath, ETAG_ATTRIBUTE_NAME);
        DeleteADS(filePath, BUCKET_ATTRIBUTE_NAME);
        DeleteADS(filePath, KEY_ATTRIBUTE_NAME);
        DeleteADS(filePath, UPLOAD_DATE_ATTRIBUTE_NAME);
        return true;
    }

    private static void WriteADS(string filePath, string streamName, string content)
    {
        var adsPath = $"{filePath}:{streamName}";
        File.WriteAllText(adsPath, content, Encoding.UTF8);
    }

    private static string? ReadADS(string filePath, string streamName)
    {
        try
        {
            var adsPath = $"{filePath}:{streamName}";
            if (File.Exists(adsPath))
            {
                return File.ReadAllText(adsPath, Encoding.UTF8);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteADS(string filePath, string streamName)
    {
        try
        {
            var adsPath = $"{filePath}:{streamName}";
            if (File.Exists(adsPath))
            {
                File.Delete(adsPath);
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    // ===== Unix Implementation (Extended Attributes) =====

    private static bool CacheETagUnix(string filePath, string etag, string bucketName, string key)
    {
        // On Linux/Mac, we can use extended attributes via system calls
        // For simplicity, we'll use a sidecar file approach as fallback
        // Real implementation would use Mono.Unix or similar

        try
        {
            var cacheDir = Path.Combine(Path.GetDirectoryName(filePath) ?? "", ".cee3cache");
            Directory.CreateDirectory(cacheDir);

            var fileName = Path.GetFileName(filePath);
            var cacheFile = Path.Combine(cacheDir, $"{fileName}.cache");

            var cacheContent = $"{etag}\n{bucketName}\n{key}\n{DateTime.UtcNow:o}";
            File.WriteAllText(cacheFile, cacheContent, Encoding.UTF8);

            // Try to hide the cache directory
            try
            {
                var dirInfo = new DirectoryInfo(cacheDir);
                dirInfo.Attributes |= FileAttributes.Hidden;
            }
            catch
            {
                // Ignore if can't hide
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static S3CacheInfo? GetCachedInfoUnix(string filePath)
    {
        try
        {
            var cacheDir = Path.Combine(Path.GetDirectoryName(filePath) ?? "", ".cee3cache");
            var fileName = Path.GetFileName(filePath);
            var cacheFile = Path.Combine(cacheDir, $"{fileName}.cache");

            if (!File.Exists(cacheFile))
            {
                return null;
            }

            var lines = File.ReadAllLines(cacheFile, Encoding.UTF8);
            if (lines.Length < 4)
            {
                return null;
            }

            DateTime? uploadDate = null;
            if (DateTime.TryParse(lines[3], out var parsed))
            {
                uploadDate = parsed;
            }

            return new S3CacheInfo
            {
                ETag = lines[0],
                BucketName = lines[1],
                Key = lines[2],
                UploadDate = uploadDate
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool ClearCacheUnix(string filePath)
    {
        try
        {
            var cacheDir = Path.Combine(Path.GetDirectoryName(filePath) ?? "", ".cee3cache");
            var fileName = Path.GetFileName(filePath);
            var cacheFile = Path.Combine(cacheDir, $"{fileName}.cache");

            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }

            // Clean up cache directory if empty
            if (Directory.Exists(cacheDir) && Directory.GetFiles(cacheDir).Length == 0)
            {
                Directory.Delete(cacheDir);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Displays cached information for a file
    /// </summary>
    public static void DisplayCachedInfo(string filePath)
    {
        var cached = GetCachedInfo(filePath);

        if (cached == null)
        {
            Console.WriteLine($"No cached S3 info for: {Path.GetFileName(filePath)}");
            return;
        }

        Console.WriteLine($"\nCached S3 Info for: {Path.GetFileName(filePath)}");
        Console.WriteLine($"  ETag: {cached.ETag}");
        Console.WriteLine($"  Bucket: {cached.BucketName}");
        Console.WriteLine($"  Key: {cached.Key}");
        Console.WriteLine($"  Uploaded: {cached.UploadDate?.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
    }
}
