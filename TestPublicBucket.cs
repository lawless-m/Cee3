using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System;
using System.Threading.Tasks;

namespace Cee3;

/// <summary>
/// Simple test program to verify public bucket access
/// </summary>
public class TestPublicBucket
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Testing Public S3 Bucket Access ===");
        Console.WriteLine("Bucket: ramsden-devstorage");
        Console.WriteLine("Region: eu-west-2");
        Console.WriteLine("Prefix: ProductImagesRamsden");
        Console.WriteLine();

        try
        {
            // Create S3 client with anonymous credentials
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.EUWest2
            };

            var s3Client = new AmazonS3Client(new AnonymousAWSCredentials(), config);
            var s3Service = new S3Service(s3Client);

            Console.WriteLine("✓ S3 Client created with anonymous credentials");
            Console.WriteLine();

            // Test 1: List objects with prefix
            Console.WriteLine("Test 1: Listing objects with prefix 'ProductImagesRamsden'...");
            var objects = await s3Service.ListObjectsAsync("ramsden-devstorage", "ProductImagesRamsden");

            Console.WriteLine($"✓ Found {objects.Count} objects");

            if (objects.Count > 0)
            {
                Console.WriteLine("\nFirst 10 objects:");
                for (int i = 0; i < Math.Min(10, objects.Count); i++)
                {
                    var obj = objects[i];
                    Console.WriteLine($"  {i + 1}. {obj.Key}");
                    Console.WriteLine($"     Size: {obj.Size:N0} bytes, Modified: {obj.LastModified}");
                }

                // Test 2: Get metadata for first object
                if (objects.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Test 2: Getting metadata for '{objects[0].Key}'...");
                    await s3Service.DisplayObjectMetadataAsync("ramsden-devstorage", objects[0].Key);
                }

                // Test 3: Check if object exists
                Console.WriteLine();
                Console.WriteLine($"Test 3: Checking if '{objects[0].Key}' exists...");
                var exists = await s3Service.ObjectExistsAsync("ramsden-devstorage", objects[0].Key);
                Console.WriteLine($"✓ Object exists: {exists}");

                // Test 4: Download first object
                Console.WriteLine();
                Console.WriteLine($"Test 4: Downloading '{objects[0].Key}'...");
                var downloadPath = $"/tmp/{Path.GetFileName(objects[0].Key)}";
                var downloaded = await s3Service.DownloadFileAsync("ramsden-devstorage", objects[0].Key, downloadPath);

                if (downloaded && File.Exists(downloadPath))
                {
                    var fileInfo = new FileInfo(downloadPath);
                    Console.WriteLine($"✓ File downloaded successfully: {fileInfo.Length:N0} bytes");
                }
            }
            else
            {
                Console.WriteLine("⚠ No objects found with the specified prefix");
                Console.WriteLine("This might mean:");
                Console.WriteLine("  - The bucket is empty");
                Console.WriteLine("  - The prefix doesn't match any objects");
                Console.WriteLine("  - The bucket doesn't allow public listing");
            }

            Console.WriteLine();
            Console.WriteLine("=== All Tests Completed Successfully! ===");
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine($"\n✗ S3 Error: {e.Message}");
            Console.WriteLine($"Error Code: {e.ErrorCode}");
            Console.WriteLine($"Status Code: {e.StatusCode}");

            if (e.ErrorCode == "AccessDenied")
            {
                Console.WriteLine("\nThe bucket may not allow public access for this operation.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"\n✗ Error: {e.Message}");
            Console.WriteLine($"Type: {e.GetType().Name}");
        }
    }
}
