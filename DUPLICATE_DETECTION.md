# Duplicate Detection and Smart Upload

Cee3 now includes intelligent duplicate detection to prevent uploading files that already exist in S3 with identical content. This saves time, bandwidth, and AWS costs.

## How It Works

### MD5 Hash Comparison

1. **Local File**: Calculate MD5 hash of the local file
2. **S3 Object**: Retrieve ETag from S3 (which is the MD5 hash for simple uploads)
3. **Compare**: If hashes match, the file is identical - skip upload!

### ETag Format

S3 ETags come in two formats:

**Simple Upload**: `"d41d8cd98f00b204e9800998ecf8427e"`
- ETag = MD5 hash of file content
- Directly comparable with local MD5

**Multipart Upload**: `"d41d8cd98f00b204e9800998ecf8427e-5"`
- ETag = MD5 of concatenated MD5s + part count
- Cannot directly compare (treated as not matching)

## Operations

### Operation 12: Smart Upload (Single File)

Uploads a single file with duplicate detection.

```
Select an operation (0-13): 12

Enter local file path: /path/to/image.jpg
Enter destination bucket name: my-bucket
Enter destination object key: images/image.jpg
Force upload even if duplicate? (y/n): n
Add custom metadata? (y/n): n

Checking if file already exists in S3...
  Calculating MD5 hash of local file...
  Calculating hash: 100.0% (2.5/2.5 MB)
✓ File already exists with identical content (ETag: d41d8cd98f00b204e9800998ecf8427e)
  Skipping upload to save time and bandwidth

Result: Duplicate - skipped
Tip: Use force upload option to override duplicate detection
```

#### Features:
- ✅ MD5 hash calculation with progress
- ✅ ETag comparison
- ✅ Force upload option
- ✅ Custom metadata support
- ✅ Clear feedback

### Operation 13: Batch Smart Upload (Multiple Files)

Uploads multiple files from a directory with duplicate detection.

```
Select an operation (0-13): 13

Enter directory path containing files to upload: /path/to/images
Enter file pattern (e.g., *.jpg, *.* for all): *.jpg
Enter destination bucket name: my-bucket
Enter key prefix (optional, e.g., images/): photos/vacation/
Force upload even if duplicates? (y/n): n

Found 50 files to upload.
Proceed with batch upload? (y/n): y

=== Batch Smart Upload ===
Files to process: 50
Target bucket: my-bucket
Key prefix: photos/vacation/
Force upload: false

[1/50] Processing: IMG_001.jpg
Checking if file already exists in S3...
  Calculating MD5 hash of local file...
  Calculating hash: 100.0% (3.2/3.2 MB)
  File does not exist in S3, uploading...
Successfully uploaded /path/to/images/IMG_001.jpg to s3://my-bucket/photos/vacation/IMG_001.jpg

[2/50] Processing: IMG_002.jpg
Checking if file already exists in S3...
  Calculating MD5 hash of local file...
  Calculating hash: 100.0% (2.8/2.8 MB)
✓ File already exists with identical content (ETag: abc123...)
  Skipping upload to save time and bandwidth

...

[50/50] Processing: IMG_050.jpg
...

=== Batch Upload Summary ===
  Uploaded: 35
  Skipped (duplicates): 12
  Failed: 3
  Total processed: 50
```

#### Features:
- ✅ Directory scanning with file patterns
- ✅ Automatic key prefix handling
- ✅ Progress reporting per file
- ✅ Comprehensive summary
- ✅ Force upload option for all files

## Use Cases

### 1. Incremental Backups

Upload only new or changed files:

```bash
# Run daily to upload only new photos
Operation 13
/media/photos
*.jpg
backup-bucket
photos/daily/
n  # Don't force - skip duplicates
```

**Benefits:**
- Only uploads changed files
- Saves upload time
- Reduces AWS transfer costs

### 2. Sync Local to S3

Keep S3 in sync with local directory:

```bash
# Sync documents folder
Operation 13
/home/user/documents
*.*
my-bucket
docs/
n  # Skip duplicates
```

**Advantages:**
- Detects unchanged files
- Only uploads modifications
- Maintains exact copies

### 3. Image Upload Pipeline

Upload product images, skip if already exists:

```bash
# Upload product images
Operation 13
/var/www/product-images
*.png
product-bucket
ProductImagesRamsden/
n  # Skip duplicates
```

**Results:**
- Prevents duplicate uploads
- Faster pipeline execution
- Consistent image keys

### 4. Development Workflow

Upload build artifacts, skip unchanged:

```bash
# Upload build outputs
Operation 13
./dist
*.*
deploy-bucket
builds/v1.2.3/
n  # Skip duplicates
```

## Performance Benefits

### Time Savings

For a 100-file batch with 60% duplicates:

**Without Duplicate Detection:**
- Upload all 100 files: ~10 minutes
- Total: 10 minutes

**With Duplicate Detection:**
- Hash 100 files: ~30 seconds
- Upload 40 new files: ~4 minutes
- Total: 4.5 minutes

**Savings: 55% faster!**

### Cost Savings

AWS S3 pricing (example):
- PUT requests: $0.005 per 1,000 requests
- Data transfer: $0.09 per GB (first 10TB)

**Example: 1,000 files, 50 MB avg, 70% duplicates**

Without deduplication:
- 1,000 PUT requests: $0.005
- 50 GB transfer: $4.50
- **Total: $4.505**

With deduplication:
- 300 PUT requests: $0.0015
- 300 GET requests: $0.0004
- 15 GB transfer: $1.35
- **Total: $1.352**

**Savings: $3.15 (70% reduction)**

### Bandwidth Savings

For large file collections:
- **Initial upload**: Full bandwidth usage
- **Subsequent uploads**: Only changed files
- **Typical savings**: 60-90% bandwidth reduction

## Configuration Options

### Force Upload

Override duplicate detection and upload anyway:

```
Force upload even if duplicate? (y/n): y
```

**When to use:**
- Update metadata without changing content
- Fix corrupted S3 objects
- Ensure latest version
- Override ETag mismatches

### Custom Metadata

Add custom metadata during smart upload:

```
Add custom metadata? (y/n): y
  Metadata key: version
  Value for 'version': 2.0
  Metadata key: author
  Value for 'author': John Doe
  Metadata key: (press Enter to finish)
```

**Note**: Metadata changes don't affect duplicate detection (only content is compared).

## Limitations

### Multipart Upload Objects

Objects uploaded via multipart have ETags like:
```
"abc123def456-5"
```

**Limitation**: Cannot verify these are duplicates
- Treated as **not matching**
- Will re-upload (to be safe)
- Typically for files > 5GB

**Workaround**: Use force=false and rely on existence check only

### Storage Class Changes

Duplicate detection compares **content only**, not:
- Storage class (STANDARD vs GLACIER)
- Metadata
- ACLs
- Tags

If you need to change these, use force upload.

### Large File Performance

Hashing very large files takes time:
- 100 MB: ~1 second
- 1 GB: ~10 seconds
- 10 GB: ~100 seconds

For files > 5GB, consider:
- Existence check only (operation 9)
- Direct upload
- AWS Transfer Acceleration

## API Usage

### Programmatic Access

```csharp
using Cee3;

var s3Service = new S3Service(s3Client);

// Single file smart upload
var (uploaded, reason) = await s3Service.SmartUploadFileAsync(
    bucketName: "my-bucket",
    key: "images/photo.jpg",
    localFilePath: "/path/to/photo.jpg",
    customMetadata: null,
    forceUpload: false
);

if (uploaded)
{
    Console.WriteLine("Uploaded successfully");
}
else if (reason.Contains("Duplicate"))
{
    Console.WriteLine("Skipped - duplicate found");
}

// Batch smart upload
var files = Directory.GetFiles("/path/to/images", "*.jpg");
var (uploadedCount, skippedCount, failedCount) =
    await s3Service.BatchSmartUploadAsync(
        bucketName: "my-bucket",
        localFilePaths: files,
        keyPrefix: "images/",
        forceUpload: false
    );

Console.WriteLine($"Uploaded: {uploadedCount}, Skipped: {skippedCount}, Failed: {failedCount}");

// Check for duplicate only
var (isDuplicate, etag) = await s3Service.CheckIfDuplicateAsync(
    bucketName: "my-bucket",
    key: "test.jpg",
    localFilePath: "/path/to/test.jpg"
);

if (isDuplicate)
{
    Console.WriteLine($"File is duplicate (ETag: {etag})");
}
```

### Hash Utility

```csharp
using Cee3;

// Calculate MD5 hash
string hash = await HashUtility.CalculateMD5Async("/path/to/file");
Console.WriteLine($"MD5: {hash}");

// With progress reporting
var fileInfo = new FileInfo(filePath);
var progress = new Progress<long>(bytes =>
{
    var pct = (double)bytes / fileInfo.Length * 100;
    Console.WriteLine($"Progress: {pct:F1}%");
});

string hashWithProgress = await HashUtility.CalculateMD5WithProgressAsync(
    filePath,
    progress
);

// Compare with S3 ETag
bool matches = HashUtility.IsETagMatch(localHash, s3ETag);

// Check if multipart
bool isMultipart = HashUtility.IsMultipartETag(etag);
```

## Best Practices

### 1. Use Consistent Key Naming

```
✓ Good: images/product-123.jpg
✗ Bad:  images/product-123-v2.jpg (creates duplicates)
```

**Strategy**: Always upload to the same key for the same logical file

### 2. Batch Uploads for Efficiency

```
✓ Good: Upload 100 files in one batch operation
✗ Bad:  Upload files one at a time in a loop
```

**Benefit**: Batch operations show aggregate progress and summary

### 3. Use Appropriate Force Settings

```
Normal workflow: force=false (skip duplicates)
Re-sync operation: force=false (update changed only)
Disaster recovery: force=true (upload everything)
Metadata update: force=true (content unchanged)
```

### 4. Monitor Skipped Files

Review batch upload summary to understand patterns:
- High skip rate: Good! Files are already backed up
- Low skip rate: Many new files or changes
- Failed uploads: Investigate permissions or network

### 5. Combine with Prefix Strategies

```bash
# Organize by date
photos/2024/10/24/image.jpg

# Organize by type
assets/images/
assets/documents/
assets/videos/

# Organize by version
releases/v1.0.0/
releases/v1.0.1/
```

## Troubleshooting

### Hash Calculation is Slow

**Symptom**: MD5 calculation takes too long

**Solutions**:
- Expected for large files (10GB+ may take minutes)
- Use SSD instead of HDD for faster I/O
- Check system load (CPU/disk)
- Consider AWS CLI sync for very large batches

### False Positives (Skipping Different Files)

**Symptom**: Files marked as duplicates when they're different

**Cause**: Extremely rare (MD5 collision probability: 1 in 2^128)

**Solution**: Use force upload to override

### ETags Don't Match

**Symptom**: "Object was uploaded as multipart, cannot verify"

**Explanation**: Multipart uploads have different ETag format

**Solution**:
- Accept that file will be re-uploaded
- Or use existence check only (operation 9)

### Permission Errors

**Symptom**: Access denied when checking metadata

**Solution**:
- Ensure `s3:GetObject` or `s3:GetObjectMetadata` permission
- Check bucket policies
- Verify IAM role/credentials

## Comparison with AWS CLI Sync

| Feature | Cee3 Smart Upload | AWS CLI Sync |
|---------|-------------------|--------------|
| **Duplicate Detection** | Yes (MD5) | Yes (size + mtime) |
| **Detection Method** | Content hash | File attributes |
| **Accuracy** | 100% (content-based) | 99% (attribute-based) |
| **Speed** | Medium (hashing) | Fast (metadata) |
| **Progress Reporting** | Per file | Aggregate |
| **Custom Metadata** | Yes | Limited |
| **Interactive** | Yes | Command-line only |
| **Batch Summary** | Yes | No |

**When to use Cee3**:
- Need content verification
- Interactive workflow
- Custom metadata
- Detailed progress per file

**When to use AWS CLI**:
- Very large directories (1000s of files)
- Speed is critical
- Automated scripts
- Already using AWS CLI tools

## Future Enhancements

Potential improvements:
- [ ] Parallel hash calculation for batch uploads
- [ ] Resume interrupted uploads
- [ ] Incremental hash for very large files
- [ ] S3 versioning integration
- [ ] Multipart upload support with chunked hashing
- [ ] Local cache of ETags for faster checks
- [ ] Dry-run mode (show what would be uploaded)

## Summary

**Smart Upload Benefits:**
- ✅ Save time (skip unchanged files)
- ✅ Save bandwidth (no redundant transfers)
- ✅ Save money (fewer PUT requests)
- ✅ Guarantee accuracy (content-based comparison)
- ✅ Interactive feedback (progress and summaries)

**Perfect for:**
- Incremental backups
- Development workflows
- Image/asset pipelines
- Regular sync operations
- Cost-conscious deployments
