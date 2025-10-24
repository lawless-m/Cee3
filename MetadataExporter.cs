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

        // Collect detailed metadata for each object
        var metadataList = new List<S3ObjectMetadata>();
        int processed = 0;

        Console.WriteLine("Fetching detailed metadata...");
        foreach (var s3Object in s3Objects)
        {
            try
            {
                var metadata = await s3Service.GetObjectMetadataAsync(bucketName, s3Object.Key);

                metadataList.Add(new S3ObjectMetadata
                {
                    BucketName = bucketName,
                    Key = s3Object.Key,
                    Size = s3Object.Size ?? 0,
                    LastModified = s3Object.LastModified ?? DateTime.UtcNow,
                    ETag = s3Object.ETag?.Trim('"'),
                    StorageClass = s3Object.StorageClass?.Value ?? "STANDARD",
                    ContentType = metadata.Headers.ContentType ?? "application/octet-stream",
                    Owner = s3Object.Owner?.DisplayName,
                    IsLatest = true,
                    VersionId = metadata.VersionId
                });

                processed++;
                if (progressCallback != null && processed % 10 == 0)
                {
                    progressCallback(processed);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not get metadata for {s3Object.Key}: {ex.Message}");
            }
        }

        Console.WriteLine($"Collected metadata for {metadataList.Count} objects");

        // Write to Parquet file
        await WriteMetadataToParquetAsync(metadataList, outputFilePath);
    }

    /// <summary>
    /// Writes metadata to a Parquet file
    /// </summary>
    private static async Task WriteMetadataToParquetAsync(
        List<S3ObjectMetadata> metadata,
        string outputFilePath)
    {
        Console.WriteLine($"Writing to Parquet file: {outputFilePath}");

        // Define Parquet schema
        var schema = new ParquetSchema(
            new DataField<string>("bucket_name"),
            new DataField<string>("key"),
            new DataField<long>("size"),
            new DataField<DateTimeOffset>("last_modified"),
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
        var lastModifieds = metadata.Select(m => new DateTimeOffset(m.LastModified)).ToArray();
        var etags = metadata.Select(m => m.ETag).ToArray();
        var storageClasses = metadata.Select(m => m.StorageClass).ToArray();
        var contentTypes = metadata.Select(m => m.ContentType).ToArray();
        var owners = metadata.Select(m => m.Owner ?? "").ToArray();
        var isLatests = metadata.Select(m => m.IsLatest).ToArray();
        var versionIds = metadata.Select(m => m.VersionId ?? "").ToArray();

        // Write Parquet file
        using (Stream fileStream = File.Create(outputFilePath))
        {
            using (var parquetWriter = await ParquetWriter.CreateAsync(schema, fileStream))
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

        var fileInfo = new FileInfo(outputFilePath);
        Console.WriteLine($"✓ Parquet file created successfully!");
        Console.WriteLine($"  File size: {fileInfo.Length:N0} bytes");
        Console.WriteLine($"  Records: {metadata.Count}");
        Console.WriteLine($"  Schema: {schema.Fields.Count} columns");
        Console.WriteLine();
        Console.WriteLine("Columns:");
        Console.WriteLine("  - bucket_name (string)");
        Console.WriteLine("  - key (string)");
        Console.WriteLine("  - size (int64)");
        Console.WriteLine("  - last_modified (timestamp)");
        Console.WriteLine("  - etag (string)");
        Console.WriteLine("  - storage_class (string)");
        Console.WriteLine("  - content_type (string)");
        Console.WriteLine("  - owner (string)");
        Console.WriteLine("  - is_latest (boolean)");
        Console.WriteLine("  - version_id (string)");
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
