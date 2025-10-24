using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Cee3;

/// <summary>
/// Service class for interacting with Amazon S3
/// Provides methods for reading metadata and performing read/write operations
/// </summary>
public class S3Service
{
    private readonly IAmazonS3 _s3Client;

    public S3Service(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    /// <summary>
    /// List all buckets in the AWS account
    /// </summary>
    public async Task<List<S3Bucket>> ListBucketsAsync()
    {
        var response = await _s3Client.ListBucketsAsync();
        return response.Buckets;
    }

    /// <summary>
    /// List objects in a specific bucket with optional prefix filter
    /// </summary>
    public async Task<List<S3Object>> ListObjectsAsync(string bucketName, string prefix = "")
    {
        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix
        };

        var objects = new List<S3Object>();
        ListObjectsV2Response response;

        do
        {
            response = await _s3Client.ListObjectsV2Async(request);
            objects.AddRange(response.S3Objects);
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);

        return objects;
    }

    /// <summary>
    /// Get metadata for a specific S3 object
    /// </summary>
    public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key)
    {
        var request = new GetObjectMetadataRequest
        {
            BucketName = bucketName,
            Key = key
        };

        return await _s3Client.GetObjectMetadataAsync(request);
    }

    /// <summary>
    /// Display detailed metadata information for an S3 object
    /// </summary>
    public async Task DisplayObjectMetadataAsync(string bucketName, string key)
    {
        try
        {
            var metadata = await GetObjectMetadataAsync(bucketName, key);

            Console.WriteLine($"\nMetadata for s3://{bucketName}/{key}:");
            Console.WriteLine($"  Content Type: {metadata.Headers.ContentType}");
            Console.WriteLine($"  Content Length: {metadata.Headers.ContentLength} bytes");
            Console.WriteLine($"  ETag: {metadata.ETag}");
            Console.WriteLine($"  Last Modified: {metadata.LastModified}");
            Console.WriteLine($"  Storage Class: {metadata.StorageClass}");

            if (metadata.Metadata.Count > 0)
            {
                Console.WriteLine("  Custom Metadata:");
                foreach (var metaKey in metadata.Metadata.Keys)
                {
                    Console.WriteLine($"    {metaKey}: {metadata.Metadata[metaKey]}");
                }
            }
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error getting metadata: {e.Message}");
        }
    }

    /// <summary>
    /// Download an S3 object to a local file
    /// </summary>
    public async Task<bool> DownloadFileAsync(string bucketName, string key, string localFilePath)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            await response.WriteResponseStreamToFileAsync(localFilePath, false, default);

            Console.WriteLine($"Successfully downloaded s3://{bucketName}/{key} to {localFilePath}");
            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error downloading file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Download an S3 object and return its contents as a string
    /// </summary>
    public async Task<string?> ReadObjectAsStringAsync(string bucketName, string key)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);

            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error reading object: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Upload a file to S3
    /// </summary>
    public async Task<bool> UploadFileAsync(string bucketName, string key, string localFilePath,
        Dictionary<string, string>? customMetadata = null)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                FilePath = localFilePath
            };

            // Add custom metadata if provided
            if (customMetadata != null)
            {
                foreach (var kvp in customMetadata)
                {
                    request.Metadata.Add(kvp.Key, kvp.Value);
                }
            }

            await _s3Client.PutObjectAsync(request);
            Console.WriteLine($"Successfully uploaded {localFilePath} to s3://{bucketName}/{key}");
            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error uploading file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Upload string content to S3
    /// </summary>
    public async Task<bool> UploadStringAsync(string bucketName, string key, string content,
        Dictionary<string, string>? customMetadata = null)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                ContentBody = content
            };

            // Add custom metadata if provided
            if (customMetadata != null)
            {
                foreach (var kvp in customMetadata)
                {
                    request.Metadata.Add(kvp.Key, kvp.Value);
                }
            }

            await _s3Client.PutObjectAsync(request);
            Console.WriteLine($"Successfully uploaded content to s3://{bucketName}/{key}");
            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error uploading content: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete an object from S3
    /// </summary>
    public async Task<bool> DeleteObjectAsync(string bucketName, string key)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
            Console.WriteLine($"Successfully deleted s3://{bucketName}/{key}");
            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error deleting object: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if an object exists in S3
    /// </summary>
    public async Task<bool> ObjectExistsAsync(string bucketName, string key)
    {
        try
        {
            await GetObjectMetadataAsync(bucketName, key);
            return true;
        }
        catch (AmazonS3Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Copy an object within S3 (can be same bucket or different bucket)
    /// </summary>
    public async Task<bool> CopyObjectAsync(string sourceBucket, string sourceKey,
        string destinationBucket, string destinationKey)
    {
        try
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = sourceBucket,
                SourceKey = sourceKey,
                DestinationBucket = destinationBucket,
                DestinationKey = destinationKey
            };

            await _s3Client.CopyObjectAsync(request);
            Console.WriteLine($"Successfully copied s3://{sourceBucket}/{sourceKey} to s3://{destinationBucket}/{destinationKey}");
            return true;
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error copying object: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a local file is a duplicate of an S3 object by comparing MD5 hashes
    /// </summary>
    public async Task<(bool isDuplicate, string? etag)> CheckIfDuplicateAsync(
        string bucketName,
        string key,
        string localFilePath)
    {
        try
        {
            // Check if object exists in S3
            if (!await ObjectExistsAsync(bucketName, key))
            {
                return (false, null);
            }

            // Get S3 object metadata
            var metadata = await GetObjectMetadataAsync(bucketName, key);
            var s3ETag = metadata.ETag;

            // Check if it's a multipart upload (can't easily compare)
            if (HashUtility.IsMultipartETag(s3ETag))
            {
                Console.WriteLine($"  Note: Object was uploaded as multipart, cannot verify by hash");
                return (false, s3ETag);
            }

            // Calculate local file MD5
            Console.WriteLine($"  Calculating MD5 hash of local file...");
            var fileInfo = new FileInfo(localFilePath);
            var progress = new Progress<long>(bytesRead =>
                HashUtility.DisplayHashProgress(bytesRead, fileInfo.Length));

            var localMD5 = await HashUtility.CalculateMD5WithProgressAsync(localFilePath, progress);

            // Compare hashes
            bool isDuplicate = HashUtility.IsETagMatch(localMD5, s3ETag);

            return (isDuplicate, s3ETag);
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"Error checking for duplicate: {e.Message}");
            return (false, null);
        }
    }

    /// <summary>
    /// Smart upload that skips uploading if file already exists with same content
    /// </summary>
    public async Task<(bool uploaded, string reason)> SmartUploadFileAsync(
        string bucketName,
        string key,
        string localFilePath,
        Dictionary<string, string>? customMetadata = null,
        bool forceUpload = false)
    {
        try
        {
            if (!File.Exists(localFilePath))
            {
                return (false, $"File not found: {localFilePath}");
            }

            // Check for duplicate unless force upload is requested
            if (!forceUpload)
            {
                Console.WriteLine($"Checking if file already exists in S3...");
                var (isDuplicate, etag) = await CheckIfDuplicateAsync(bucketName, key, localFilePath);

                if (isDuplicate)
                {
                    Console.WriteLine($"✓ File already exists with identical content (ETag: {etag})");
                    Console.WriteLine($"  Skipping upload to save time and bandwidth");
                    return (false, "Duplicate - skipped");
                }

                if (etag != null)
                {
                    Console.WriteLine($"  File exists but content differs, uploading...");
                }
                else
                {
                    Console.WriteLine($"  File does not exist in S3, uploading...");
                }
            }

            // Proceed with upload
            bool success = await UploadFileAsync(bucketName, key, localFilePath, customMetadata);

            if (success)
            {
                return (true, "Uploaded successfully");
            }
            else
            {
                return (false, "Upload failed");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in smart upload: {e.Message}");
            return (false, $"Error: {e.Message}");
        }
    }

    /// <summary>
    /// Batch upload multiple files with duplicate detection
    /// </summary>
    public async Task<(int uploaded, int skipped, int failed)> BatchSmartUploadAsync(
        string bucketName,
        string[] localFilePaths,
        string keyPrefix = "",
        bool forceUpload = false)
    {
        int uploaded = 0;
        int skipped = 0;
        int failed = 0;

        Console.WriteLine($"\n=== Batch Smart Upload ===");
        Console.WriteLine($"Files to process: {localFilePaths.Length}");
        Console.WriteLine($"Target bucket: {bucketName}");
        Console.WriteLine($"Key prefix: {(string.IsNullOrEmpty(keyPrefix) ? "(none)" : keyPrefix)}");
        Console.WriteLine($"Force upload: {forceUpload}");
        Console.WriteLine();

        for (int i = 0; i < localFilePaths.Length; i++)
        {
            var filePath = localFilePaths[i];
            var fileName = Path.GetFileName(filePath);
            var key = string.IsNullOrEmpty(keyPrefix)
                ? fileName
                : $"{keyPrefix.TrimEnd('/')}/{fileName}";

            Console.WriteLine($"[{i + 1}/{localFilePaths.Length}] Processing: {fileName}");

            try
            {
                var (wasUploaded, reason) = await SmartUploadFileAsync(
                    bucketName,
                    key,
                    filePath,
                    null,
                    forceUpload);

                if (wasUploaded)
                {
                    uploaded++;
                }
                else if (reason.StartsWith("Duplicate"))
                {
                    skipped++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error processing {fileName}: {ex.Message}");
                failed++;
            }

            Console.WriteLine();
        }

        // Summary
        Console.WriteLine("=== Batch Upload Summary ===");
        Console.WriteLine($"  Uploaded: {uploaded}");
        Console.WriteLine($"  Skipped (duplicates): {skipped}");
        Console.WriteLine($"  Failed: {failed}");
        Console.WriteLine($"  Total processed: {localFilePaths.Length}");

        return (uploaded, skipped, failed);
    }
}
