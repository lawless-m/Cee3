using Amazon.S3.Model;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Cee3;

/// <summary>
/// Service for exporting S3 object metadata to Parquet files
/// </summary>
public class MetadataExporter
{
    /// <summary>
    /// Represents S3 object metadata in a structured format
    /// </summary>
    public class S3ObjectMetadata
    {
        public string? BucketName { get; set; }
        public string? Key { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? ETag { get; set; }
        public string? StorageClass { get; set; }
        public string? ContentType { get; set; }
        public string? Owner { get; set; }
        public bool IsLatest { get; set; }
        public string? VersionId { get; set; }
    }

    /// <summary>
    /// Infers content type from file extension
    /// </summary>
    private static string InferContentType(string key)
    {
        var extension = Path.GetExtension(key).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Collects metadata from S3 objects and exports to Parquet file
    /// </summary>
    public static async Task ExportMetadataToParquetAsync(
        S3Service s3Service,
        string bucketName,
        string outputFilePath,
        string prefix = "",
        Action<int>? progressCallback = null)
    {
        Console.WriteLine($"Collecting metadata from bucket: {bucketName}");
        if (!string.IsNullOrEmpty(prefix))
        {
            Console.WriteLine($"Using prefix filter: {prefix}");
        }

        // Get all objects in the bucket
        var s3Objects = await s3Service.ListObjectsAsync(bucketName, prefix);
        Console.WriteLine($"Found {s3Objects.Count} objects");

        if (s3Objects.Count == 0)
        {
            Console.WriteLine("No objects to export.");
            return;
        }

        // Process and write in batches to manage memory for large datasets
        const int batchSize = 50000; // Per Parquet skill recommendation
        var metadataBatch = new List<S3ObjectMetadata>(batchSize);
        int processed = 0;
        bool isFirstBatch = true;

        Console.WriteLine($"Processing and writing in batches of {batchSize:N0}...");

        foreach (var s3Object in s3Objects)
        {
            try
            {
                // Infer content type from file extension (avoid making 1.4M API calls!)
                string contentType = InferContentType(s3Object.Key);

                metadataBatch.Add(new S3ObjectMetadata
                {
                    BucketName = bucketName,
                    Key = s3Object.Key,
                    Size = s3Object.Size ?? 0,
                    LastModified = s3Object.LastModified ?? DateTime.UtcNow,
                    ETag = s3Object.ETag?.Trim('"'),
                    StorageClass = s3Object.StorageClass?.Value ?? "STANDARD",
                    ContentType = contentType,
                    Owner = s3Object.Owner?.DisplayName,
                    IsLatest = true,
                    VersionId = null // Not available from ListObjects
                });

                // Write batch when it reaches batchSize
                if (metadataBatch.Count >= batchSize)
                {
                    await WriteBatchToParquetAsync(metadataBatch, outputFilePath, isFirstBatch);
                    processed += metadataBatch.Count;

                    if (progressCallback != null)
                    {
                        progressCallback(processed);
                    }

                    // Clear batch and force GC for large datasets (per Parquet skill)
                    metadataBatch.Clear();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    isFirstBatch = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not process metadata for {s3Object.Key}: {ex.Message}");
            }
        }

        // Write remaining records
        if (metadataBatch.Count > 0)
        {
            await WriteBatchToParquetAsync(metadataBatch, outputFilePath, isFirstBatch);
            processed += metadataBatch.Count;
            metadataBatch.Clear();
        }

        Console.WriteLine($"✓ Processed and wrote {processed:N0} objects to Parquet file");
    }

    /// <summary>
    /// Writes a batch of metadata to Parquet file (append mode for subsequent batches)
    /// </summary>
    private static async Task WriteBatchToParquetAsync(
        List<S3ObjectMetadata> metadata,
        string outputFilePath,
        bool isFirstBatch)
    {
        // Define Parquet schema
        var schema = new ParquetSchema(
            new DataField<string>("bucket_name"),
            new DataField<string>("key"),
            new DataField<long>("size"),
            new DataField<DateTime>("last_modified"),
            new DataField<string>("etag"),
            new DataField<string>("storage_class"),
            new DataField<string>("content_type"),
            new DataField<string>("owner"),
            new DataField<bool>("is_latest"),
            new DataField<string>("version_id")
        );

        // Prepare data columns
        var bucketNames = metadata.Select(m => m.BucketName).ToArray();
        var keys = metadata.Select(m => m.Key).ToArray();
        var sizes = metadata.Select(m => m.Size).ToArray();
        var lastModifieds = metadata.Select(m => m.LastModified).ToArray();
        var etags = metadata.Select(m => m.ETag).ToArray();
        var storageClasses = metadata.Select(m => m.StorageClass).ToArray();
        var contentTypes = metadata.Select(m => m.ContentType).ToArray();
        var owners = metadata.Select(m => m.Owner ?? "").ToArray();
        var isLatests = metadata.Select(m => m.IsLatest).ToArray();
        var versionIds = metadata.Select(m => m.VersionId ?? "").ToArray();

        // Open file for writing (create new or append)
        // Per Parquet skill: Use FileAccess.ReadWrite when appending because ParquetWriter
        // needs to read existing file metadata to validate schema compatibility
        FileMode fileMode = isFirstBatch ? FileMode.Create : FileMode.Open;
        FileAccess fileAccess = isFirstBatch ? FileAccess.Write : FileAccess.ReadWrite;

        using (Stream fileStream = new FileStream(outputFilePath, fileMode, fileAccess))
        {
            using (var parquetWriter = await ParquetWriter.CreateAsync(schema, fileStream, append: !isFirstBatch))
            {
                // Create row group
                using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                {
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[0], bucketNames));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[1], keys));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[2], sizes));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[3], lastModifieds));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[4], etags));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[5], storageClasses));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[6], contentTypes));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[7], owners));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[8], isLatests));
                    await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[9], versionIds));
                }
            }
        }

        // Show summary only on first batch
        if (isFirstBatch)
        {
            Console.WriteLine($"✓ Started writing to Parquet file: {outputFilePath}");
            Console.WriteLine($"  Schema: {schema.Fields.Count} columns");
        }
    }

    /// <summary>
    /// Reads and displays basic information about a Parquet file
    /// </summary>
    public static async Task DisplayParquetInfoAsync(string parquetFilePath)
    {
        if (!File.Exists(parquetFilePath))
        {
            Console.WriteLine($"File not found: {parquetFilePath}");
            return;
        }

        Console.WriteLine($"Reading Parquet file: {parquetFilePath}");
        Console.WriteLine();

        using (Stream fileStream = File.OpenRead(parquetFilePath))
        {
            using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
            {
                Console.WriteLine($"Parquet File Information:");
                Console.WriteLine($"  Row groups: {parquetReader.RowGroupCount}");
                Console.WriteLine($"  Schema fields: {parquetReader.Schema.Fields.Count}");
                Console.WriteLine();

                Console.WriteLine("Schema:");
                foreach (var field in parquetReader.Schema.Fields)
                {
                    Console.WriteLine($"  - {field.Name} ({field.GetType().Name})");
                }
                Console.WriteLine();

                // Read first row group to get row count
                if (parquetReader.RowGroupCount > 0)
                {
                    using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(0))
                    {
                        var firstColumn = await groupReader.ReadColumnAsync(parquetReader.Schema.DataFields[0]);
                        Console.WriteLine($"Total records: {firstColumn.Data.Length}");
                    }
                }

                // Display first 10 records
                Console.WriteLine();
                Console.WriteLine("First 10 records:");
                Console.WriteLine();

                for (int i = 0; i < Math.Min(1, parquetReader.RowGroupCount); i++)
                {
                    using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                    {
                        // Read all columns
                        var bucketNames = await groupReader.ReadColumnAsync(parquetReader.Schema.DataFields[0]);
                        var keys = await groupReader.ReadColumnAsync(parquetReader.Schema.DataFields[1]);
                        var sizes = await groupReader.ReadColumnAsync(parquetReader.Schema.DataFields[2]);
                        var lastModifieds = await groupReader.ReadColumnAsync(parquetReader.Schema.DataFields[3]);

                        for (int j = 0; j < Math.Min(10, bucketNames.Data.Length); j++)
                        {
                            Console.WriteLine($"Record {j + 1}:");
                            Console.WriteLine($"  Bucket: {bucketNames.Data.GetValue(j)}");
                            Console.WriteLine($"  Key: {keys.Data.GetValue(j)}");
                            Console.WriteLine($"  Size: {sizes.Data.GetValue(j):N0} bytes");
                            Console.WriteLine($"  Last Modified: {lastModifieds.Data.GetValue(j)}");
                            Console.WriteLine();
                        }
                    }
                }
            }
        }
    }
}
