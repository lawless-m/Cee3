using Amazon.S3;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Microsoft.Extensions.Configuration;
using Cee3;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddJsonFile("appsettings.PublicTest.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Get AWS configuration
var awsProfile = configuration["AWS:Profile"] ?? "default";
var awsRegion = configuration["AWS:Region"] ?? "us-east-1";
var useLocalEndpoint = bool.Parse(configuration["S3:UseLocalEndpoint"] ?? "false");
var useAnonymousCredentials = bool.Parse(configuration["S3:UseAnonymousCredentials"] ?? "false");
var localEndpoint = configuration["S3:LocalEndpoint"] ?? "http://localhost:9000";
var localAccessKey = configuration["S3:LocalAccessKey"] ?? "minioadmin";
var localSecretKey = configuration["S3:LocalSecretKey"] ?? "minioadmin";

Console.WriteLine("=== Cee3 - Amazon S3 Access Tool ===");
Console.WriteLine($"AWS Profile: {awsProfile}");
Console.WriteLine($"AWS Region: {awsRegion}");

if (useLocalEndpoint)
{
    Console.WriteLine($"Mode: LOCAL TESTING (MinIO)");
    Console.WriteLine($"Endpoint: {localEndpoint}");
}
else if (useAnonymousCredentials)
{
    Console.WriteLine($"Mode: PUBLIC READ (Anonymous)");
}
else
{
    Console.WriteLine($"Mode: AWS S3");
}

Console.WriteLine();

try
{
    IAmazonS3 s3Client;

    // Use local endpoint (MinIO) for testing
    if (useLocalEndpoint)
    {
        Console.WriteLine("✓ Connecting to local S3-compatible server (MinIO)...");

        var config = new AmazonS3Config
        {
            ServiceURL = localEndpoint,
            ForcePathStyle = true, // Required for MinIO
            AuthenticationRegion = awsRegion
        };

        var credentials = new Amazon.Runtime.BasicAWSCredentials(localAccessKey, localSecretKey);
        s3Client = new AmazonS3Client(credentials, config);
    }
    // Use anonymous credentials for public buckets
    else if (useAnonymousCredentials)
    {
        Console.WriteLine("✓ Connecting with anonymous credentials (public read access)...");

        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion)
        };

        s3Client = new AmazonS3Client(new Amazon.Runtime.AnonymousAWSCredentials(), config);
    }
    // Use real AWS S3 with credentials
    else
    {
        // Create S3 client using AWS credentials
        // This will look for credentials in the following order:
        // 1. Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
        // 2. AWS credentials file (~/.aws/credentials)
        // 3. IAM role (if running on EC2)

        var credentialProfileStoreChain = new CredentialProfileStoreChain();
        AWSCredentials? credentials = null;

        if (credentialProfileStoreChain.TryGetAWSCredentials(awsProfile, out credentials))
        {
            Console.WriteLine($"✓ Successfully loaded AWS credentials from profile '{awsProfile}'");
        }
        else
        {
            Console.WriteLine($"⚠ Could not load profile '{awsProfile}', attempting to use environment variables or default credentials...");
            // Try to get credentials from environment variables or instance profile
            try
            {
                credentials = new Amazon.Runtime.EnvironmentVariablesAWSCredentials();
            }
            catch
            {
                // If environment variables aren't set, this will throw an exception
                // In that case, let the S3 client use its default credential chain
                credentials = null!;
            }
        }

        s3Client = new AmazonS3Client(credentials, Amazon.RegionEndpoint.GetBySystemName(awsRegion));
    }

    var s3Service = new S3Service(s3Client);

    Console.WriteLine("\n=== Available Operations ===");
    Console.WriteLine("1. List all buckets");
    Console.WriteLine("2. List objects in a bucket");
    Console.WriteLine("3. Get object metadata");
    Console.WriteLine("4. Download an object");
    Console.WriteLine("5. Upload a file");
    Console.WriteLine("6. Upload text content");
    Console.WriteLine("7. Delete an object");
    Console.WriteLine("8. Copy an object");
    Console.WriteLine("9. Check if object exists");
    Console.WriteLine("10. Export metadata to Parquet file");
    Console.WriteLine("11. Display Parquet file info");
    Console.WriteLine("0. Exit");
    Console.WriteLine();

    bool running = true;
    while (running)
    {
        Console.Write("Select an operation (0-11): ");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await ListBuckets(s3Service);
                break;
            case "2":
                await ListObjects(s3Service);
                break;
            case "3":
                await GetMetadata(s3Service);
                break;
            case "4":
                await DownloadObject(s3Service);
                break;
            case "5":
                await UploadFile(s3Service);
                break;
            case "6":
                await UploadText(s3Service);
                break;
            case "7":
                await DeleteObject(s3Service);
                break;
            case "8":
                await CopyObject(s3Service);
                break;
            case "9":
                await CheckExists(s3Service);
                break;
            case "10":
                await ExportMetadataToParquet(s3Service);
                break;
            case "11":
                await DisplayParquetInfo();
                break;
            case "0":
                running = false;
                Console.WriteLine("Goodbye!");
                break;
            default:
                Console.WriteLine("Invalid choice. Please try again.");
                break;
        }

        if (running)
        {
            Console.WriteLine("\n" + new string('-', 50) + "\n");
        }
    }
}
catch (AmazonServiceException e)
{
    Console.WriteLine($"\n✗ AWS Service Error: {e.Message}");
    Console.WriteLine($"Error Code: {e.ErrorCode}");
}
catch (Exception e)
{
    Console.WriteLine($"\n✗ Error: {e.Message}");
    Console.WriteLine("\nMake sure you have AWS credentials configured.");
    Console.WriteLine("You can set them up using 'aws configure' or by setting environment variables:");
    Console.WriteLine("  - AWS_ACCESS_KEY_ID");
    Console.WriteLine("  - AWS_SECRET_ACCESS_KEY");
    Console.WriteLine("  - AWS_REGION (optional)");
}

// Helper methods for each operation

async Task ListBuckets(S3Service service)
{
    Console.WriteLine("\n=== Listing All Buckets ===");
    var buckets = await service.ListBucketsAsync();

    if (buckets.Count == 0)
    {
        Console.WriteLine("No buckets found.");
    }
    else
    {
        Console.WriteLine($"Found {buckets.Count} bucket(s):");
        foreach (var bucket in buckets)
        {
            Console.WriteLine($"  • {bucket.BucketName} (Created: {bucket.CreationDate})");
        }
    }
}

async Task ListObjects(S3Service service)
{
    Console.Write("\nEnter bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter prefix (optional, press Enter to skip): ");
    var prefix = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(bucketName))
    {
        Console.WriteLine("Bucket name is required.");
        return;
    }

    Console.WriteLine($"\n=== Listing Objects in {bucketName} ===");
    var objects = await service.ListObjectsAsync(bucketName, prefix ?? "");

    if (objects.Count == 0)
    {
        Console.WriteLine("No objects found.");
    }
    else
    {
        Console.WriteLine($"Found {objects.Count} object(s):");
        foreach (var obj in objects)
        {
            Console.WriteLine($"  • {obj.Key}");
            Console.WriteLine($"    Size: {obj.Size:N0} bytes, Modified: {obj.LastModified}");
        }
    }
}

async Task GetMetadata(S3Service service)
{
    Console.Write("\nEnter bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter object key: ");
    var key = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
    {
        Console.WriteLine("Both bucket name and object key are required.");
        return;
    }

    await service.DisplayObjectMetadataAsync(bucketName, key);
}

async Task DownloadObject(S3Service service)
{
    Console.Write("\nEnter bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter object key: ");
    var key = Console.ReadLine();

    Console.Write("Enter local file path to save: ");
    var localPath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(localPath))
    {
        Console.WriteLine("Bucket name, object key, and local path are all required.");
        return;
    }

    await service.DownloadFileAsync(bucketName, key, localPath);
}

async Task UploadFile(S3Service service)
{
    Console.Write("\nEnter local file path: ");
    var localPath = Console.ReadLine();

    Console.Write("Enter destination bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter destination object key: ");
    var key = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(localPath) || string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
    {
        Console.WriteLine("All fields are required.");
        return;
    }

    if (!File.Exists(localPath))
    {
        Console.WriteLine($"File not found: {localPath}");
        return;
    }

    Console.Write("Add custom metadata? (y/n): ");
    Dictionary<string, string>? metadata = null;

    if (Console.ReadLine()?.ToLower() == "y")
    {
        metadata = new Dictionary<string, string>();
        bool addingMetadata = true;

        while (addingMetadata)
        {
            Console.Write("  Metadata key (or press Enter to finish): ");
            var metaKey = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(metaKey))
            {
                addingMetadata = false;
            }
            else
            {
                Console.Write($"  Value for '{metaKey}': ");
                var metaValue = Console.ReadLine();
                metadata[metaKey] = metaValue ?? "";
            }
        }
    }

    await service.UploadFileAsync(bucketName, key, localPath, metadata);
}

async Task UploadText(S3Service service)
{
    Console.Write("\nEnter text content: ");
    var content = Console.ReadLine();

    Console.Write("Enter destination bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter destination object key: ");
    var key = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
    {
        Console.WriteLine("All fields are required.");
        return;
    }

    await service.UploadStringAsync(bucketName, key, content);
}

async Task DeleteObject(S3Service service)
{
    Console.Write("\nEnter bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter object key to delete: ");
    var key = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
    {
        Console.WriteLine("Both bucket name and object key are required.");
        return;
    }

    Console.Write($"Are you sure you want to delete s3://{bucketName}/{key}? (y/n): ");
    if (Console.ReadLine()?.ToLower() == "y")
    {
        await service.DeleteObjectAsync(bucketName, key);
    }
    else
    {
        Console.WriteLine("Delete cancelled.");
    }
}

async Task CopyObject(S3Service service)
{
    Console.Write("\nEnter source bucket name: ");
    var sourceBucket = Console.ReadLine();

    Console.Write("Enter source object key: ");
    var sourceKey = Console.ReadLine();

    Console.Write("Enter destination bucket name: ");
    var destBucket = Console.ReadLine();

    Console.Write("Enter destination object key: ");
    var destKey = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(sourceBucket) || string.IsNullOrWhiteSpace(sourceKey) ||
        string.IsNullOrWhiteSpace(destBucket) || string.IsNullOrWhiteSpace(destKey))
    {
        Console.WriteLine("All fields are required.");
        return;
    }

    await service.CopyObjectAsync(sourceBucket, sourceKey, destBucket, destKey);
}

async Task CheckExists(S3Service service)
{
    Console.Write("\nEnter bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter object key: ");
    var key = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
    {
        Console.WriteLine("Both bucket name and object key are required.");
        return;
    }

    var exists = await service.ObjectExistsAsync(bucketName, key);

    if (exists)
    {
        Console.WriteLine($"✓ Object exists: s3://{bucketName}/{key}");
    }
    else
    {
        Console.WriteLine($"✗ Object does not exist: s3://{bucketName}/{key}");
    }
}

async Task ExportMetadataToParquet(S3Service service)
{
    Console.Write("\nEnter bucket name: ");
    var bucketName = Console.ReadLine();

    Console.Write("Enter prefix (optional, press Enter to skip): ");
    var prefix = Console.ReadLine();

    Console.Write("Enter output Parquet file path (e.g., /tmp/metadata.parquet): ");
    var outputPath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine("Bucket name and output path are required.");
        return;
    }

    try
    {
        await MetadataExporter.ExportMetadataToParquetAsync(
            service,
            bucketName,
            outputPath,
            prefix ?? "",
            (count) => Console.WriteLine($"  Processed {count} objects..."));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error exporting metadata: {ex.Message}");
    }
}

async Task DisplayParquetInfo()
{
    Console.Write("\nEnter Parquet file path: ");
    var filePath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(filePath))
    {
        Console.WriteLine("File path is required.");
        return;
    }

    try
    {
        await MetadataExporter.DisplayParquetInfoAsync(filePath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading Parquet file: {ex.Message}");
    }
}
