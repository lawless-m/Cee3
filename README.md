# Cee3 - Amazon S3 Access Tool

A .NET 8.0 console application for interacting with Amazon S3, providing full read/write access to S3 objects and metadata operations.

## Features

### Core Operations
- **List Buckets** - View all S3 buckets in your AWS account
- **List Objects** - Browse objects in a bucket with optional prefix filtering
- **Read Metadata** - View detailed metadata for S3 objects including:
  - Content type and length
  - ETag and last modified date
  - Storage class
  - Custom metadata
- **Download Files** - Download S3 objects to local files
- **Upload Files** - Upload local files to S3 with optional custom metadata
- **Upload Text** - Upload text content directly to S3
- **Delete Objects** - Remove objects from S3 buckets
- **Copy Objects** - Copy objects within or between S3 buckets
- **Check Existence** - Verify if an object exists in S3

### Advanced Features
- **Smart Upload** - Upload files with automatic duplicate detection (saves bandwidth and costs!)
- **Batch Smart Upload** - Upload multiple files from a directory, skipping duplicates
- **Export to Parquet** - Export S3 object metadata to Apache Parquet files for analytics
- **View Parquet Files** - Display information about exported Parquet files

### Duplicate Detection with ETag Caching
Prevent uploading files that already exist with identical content:
- MD5 hash verification
- **ETag caching** in file extended attributes (NTFS ADS/xattr) for ultra-fast checks
- Eliminates S3 API calls for unchanged files
- Automatic skip of unchanged files
- Progress reporting
- Batch processing support
- See [DUPLICATE_DETECTION.md](DUPLICATE_DETECTION.md) and [ETAG_CACHING.md](ETAG_CACHING.md) for details

## Prerequisites

- .NET 8.0 SDK
- AWS Account with S3 access
- AWS credentials configured (see Configuration section)

## Installation

1. Clone this repository:
   ```bash
   git clone <repository-url>
   cd Cee3
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

## Configuration

The application **requires** a configuration file specified via the `--config` parameter. No fallback credentials are supported.

Create a JSON configuration file (e.g., `s3-config.json`):

```json
{
  "AWS": {
    "Region": "eu-west-2",
    "AccessKeyId": "YOUR_ACCESS_KEY_ID",
    "SecretAccessKey": "YOUR_SECRET_ACCESS_KEY"
  },
  "S3": {
    "DefaultBucket": "my-bucket-name"
  }
}
```

**Required fields:**
- `AWS:Region` - AWS region (e.g., "us-east-1", "eu-west-2")
- `AWS:AccessKeyId` - Your AWS access key ID
- `AWS:SecretAccessKey` - Your AWS secret access key

**Security Note:** Keep your configuration file secure and never commit it to version control. Store it in a safe location outside your project directory.

## Usage

### Interactive Mode

Run the application with a configuration file to access the interactive menu:

```bash
dotnet run -- --config /path/to/s3-config.json
```

You'll be presented with an interactive menu:

```
=== Cee3 - Amazon S3 Access Tool ===
AWS Region: eu-west-2
Mode: AWS S3 (using configured credentials)

=== Available Operations ===
1. List all buckets
2. List objects in a bucket
3. Get object metadata
4. Download an object
5. Upload a file
6. Upload text content
7. Delete an object
8. Copy an object
9. Check if object exists
10. Export metadata to Parquet file
11. Display Parquet file info
12. Smart upload (skip duplicates)
13. Batch smart upload (multiple files)
14. View cached ETag info for file
0. Exit

Select an operation (0-14):
```

### Direct Export Mode (Non-Interactive)

Export S3 bucket metadata directly to a Parquet file without the interactive menu:

```bash
# Export entire bucket
dotnet run -- --config s3-config.json --export-bucket mybucket --export-output metadata.parquet

# Export with prefix filter
dotnet run -- --config s3-config.json --export-bucket mybucket --export-prefix images/ --export-output images.parquet
```

**Parameters:**
- `--config` - Path to configuration file (required)
- `--export-bucket` - S3 bucket name to export
- `--export-output` - Output Parquet file path
- `--export-prefix` - Optional: Only export objects with this prefix

**Performance:** Exports process metadata for millions of objects in minutes using optimized batch processing (50,000 records per batch) without individual API calls per object.

## Example Operations

#### List All Buckets
```
Select an operation (0-9): 1

=== Listing All Buckets ===
Found 3 bucket(s):
  • my-data-bucket (Created: 2024-01-15 10:30:00)
  • my-logs-bucket (Created: 2024-02-20 14:15:30)
  • my-backup-bucket (Created: 2024-03-10 09:45:12)
```

#### Get Object Metadata
```
Select an operation (0-9): 3

Enter bucket name: my-data-bucket
Enter object key: documents/report.pdf

Metadata for s3://my-data-bucket/documents/report.pdf:
  Content Type: application/pdf
  Content Length: 1048576 bytes
  ETag: "abc123def456"
  Last Modified: 2024-10-24 12:30:00
  Storage Class: STANDARD
  Custom Metadata:
    author: John Doe
    department: Engineering
```

#### Upload a File with Custom Metadata
```
Select an operation (0-9): 5

Enter local file path: /path/to/local/file.txt
Enter destination bucket name: my-data-bucket
Enter destination object key: uploads/file.txt
Add custom metadata? (y/n): y
  Metadata key (or press Enter to finish): author
  Value for 'author': Jane Smith
  Metadata key (or press Enter to finish): project
  Value for 'project': Q4-2024
  Metadata key (or press Enter to finish):

Successfully uploaded /path/to/local/file.txt to s3://my-data-bucket/uploads/file.txt
```

## Project Structure

```
Cee3/
├── Program.cs              # Main application entry point with interactive menu
├── S3Service.cs            # S3 operations service class
├── Cee3.csproj            # Project configuration
├── appsettings.json       # Application configuration
├── .gitignore             # Git ignore rules (protects credentials)
├── README.md              # This file
└── LICENSE                # License information
```

## API Reference

### S3Service Class

The `S3Service` class provides the following methods:

#### Bucket Operations

- `Task<List<S3Bucket>> ListBucketsAsync()`
  - Returns a list of all S3 buckets

#### Object Listing

- `Task<List<S3Object>> ListObjectsAsync(string bucketName, string prefix = "")`
  - Lists objects in a bucket with optional prefix filter
  - Automatically handles pagination for large result sets

#### Metadata Operations

- `Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key)`
  - Retrieves metadata for a specific object

- `Task DisplayObjectMetadataAsync(string bucketName, string key)`
  - Displays formatted metadata information to console

#### Read Operations

- `Task<bool> DownloadFileAsync(string bucketName, string key, string localFilePath)`
  - Downloads an S3 object to a local file

- `Task<string?> ReadObjectAsStringAsync(string bucketName, string key)`
  - Reads an S3 object and returns its contents as a string

#### Write Operations

- `Task<bool> UploadFileAsync(string bucketName, string key, string localFilePath, Dictionary<string, string>? customMetadata = null)`
  - Uploads a local file to S3 with optional custom metadata

- `Task<bool> UploadStringAsync(string bucketName, string key, string content, Dictionary<string, string>? customMetadata = null)`
  - Uploads string content to S3 with optional custom metadata

#### Delete Operations

- `Task<bool> DeleteObjectAsync(string bucketName, string key)`
  - Deletes an object from S3

#### Copy Operations

- `Task<bool> CopyObjectAsync(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey)`
  - Copies an object within S3

#### Utility Operations

- `Task<bool> ObjectExistsAsync(string bucketName, string key)`
  - Checks if an object exists in S3

## Security Considerations

- **Never commit AWS credentials** to version control
- The `.gitignore` file is configured to exclude credential files
- Use IAM roles with least-privilege permissions
- Consider using AWS Secrets Manager for production deployments
- Enable S3 bucket versioning and encryption where appropriate

## Dependencies

- **AWSSDK.S3** (4.0.7.14) - AWS SDK for .NET S3 operations
- **Microsoft.Extensions.Configuration** (9.0.10) - Configuration framework
- **Microsoft.Extensions.Configuration.Json** (9.0.10) - JSON configuration support
- **Microsoft.Extensions.Configuration.EnvironmentVariables** (9.0.10) - Environment variable support

## Building and Publishing

### Build for Debug
```bash
dotnet build
```

### Build for Release
```bash
dotnet build -c Release
```

### Publish as Self-Contained Executable

Linux:
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

Windows:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

macOS:
```bash
dotnet publish -c Release -r osx-x64 --self-contained
```

## Troubleshooting

### "Unable to get IAM security credentials from EC2 Instance Metadata Service"

This error occurs when AWS credentials are not configured. Ensure you've set up credentials using one of the methods in the Configuration section.

### "Access Denied" errors

Verify that your AWS IAM user/role has the necessary S3 permissions:
- `s3:ListBucket` - For listing objects
- `s3:GetObject` - For reading objects
- `s3:PutObject` - For uploading objects
- `s3:DeleteObject` - For deleting objects
- `s3:GetObjectMetadata` - For reading metadata

### Connection timeout issues

Check your network connectivity and ensure your security groups/firewall allow HTTPS (port 443) traffic to AWS.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License

See the LICENSE file for details.

## Support

For issues related to:
- **AWS SDK**: Visit [AWS SDK for .NET Documentation](https://docs.aws.amazon.com/sdk-for-net/)
- **AWS S3**: Visit [Amazon S3 Documentation](https://docs.aws.amazon.com/s3/)
- **This project**: Open an issue in the repository

## Version History

- **1.0.0** (2024-10-24)
  - Initial release
  - Full S3 read/write operations
  - Metadata support
  - Interactive CLI interface
  - Multi-source credential configuration
