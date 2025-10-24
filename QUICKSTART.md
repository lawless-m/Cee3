# Cee3 Quick Start Guide

## Testing Without AWS Credentials (Recommended for First Time)

### Step 1: Start MinIO and Create Test Data

```bash
./setup-test-data.sh
```

This will:
- Start MinIO in Docker (if Docker is running)
- Create test buckets: `test-bucket`, `my-data-bucket`, `backup-bucket`
- Upload sample files for testing
- Display MinIO Console URL: http://localhost:9001

### Step 2: Run Cee3

The application will automatically detect the local configuration:

```bash
dotnet run
```

You should see:
```
=== Cee3 - Amazon S3 Access Tool ===
AWS Profile: local
AWS Region: us-east-1
Mode: LOCAL TESTING (MinIO)
Endpoint: http://localhost:9000

✓ Connecting to local S3-compatible server (MinIO)...
```

### Step 3: Try Some Operations

**List Buckets (Operation 1):**
```
Select an operation (0-9): 1

=== Listing All Buckets ===
Found 3 bucket(s):
  • test-bucket (Created: ...)
  • my-data-bucket (Created: ...)
  • backup-bucket (Created: ...)
```

**List Objects (Operation 2):**
```
Select an operation (0-9): 2
Enter bucket name: test-bucket
Enter prefix (optional, press Enter to skip):

=== Listing Objects in test-bucket ===
Found 2 object(s):
  • sample.txt
    Size: 123 bytes, Modified: ...
  • documents/readme.md
    Size: 456 bytes, Modified: ...
```

**Get Metadata (Operation 3):**
```
Select an operation (0-9): 3
Enter bucket name: test-bucket
Enter object key: sample.txt

Metadata for s3://test-bucket/sample.txt:
  Content Type: text/plain
  Content Length: 123 bytes
  ETag: "abc123..."
  Last Modified: ...
  Storage Class: STANDARD
```

**Upload a File (Operation 5):**
```
Select an operation (0-9): 5
Enter local file path: README.md
Enter destination bucket name: test-bucket
Enter destination object key: my-readme.md
Add custom metadata? (y/n): n

Successfully uploaded README.md to s3://test-bucket/my-readme.md
```

### Step 4: Explore MinIO Console (Optional)

Open http://localhost:9001 in your browser:
- Username: `minioadmin`
- Password: `minioadmin`

You can visually browse buckets, upload/download files, and see all the changes you made through Cee3!

### Step 5: Stop MinIO When Done

```bash
docker-compose down
```

---

## Using with Real AWS S3

### Step 1: Configure AWS Credentials

**Option A - AWS CLI (Recommended):**
```bash
aws configure
```
Enter your AWS Access Key ID, Secret Access Key, and default region.

**Option B - Environment Variables:**
```bash
export AWS_ACCESS_KEY_ID=your_access_key
export AWS_SECRET_ACCESS_KEY=your_secret_key
export AWS_REGION=us-east-1
```

**Option C - Edit appsettings.json:**
```json
{
  "AWS": {
    "Profile": "default",
    "Region": "us-east-1"
  },
  "S3": {
    "UseLocalEndpoint": false
  }
}
```

### Step 2: Run Cee3

```bash
dotnet run
```

You should see:
```
=== Cee3 - Amazon S3 Access Tool ===
AWS Profile: default
AWS Region: us-east-1
Mode: AWS S3

✓ Successfully loaded AWS credentials from profile 'default'
```

### Step 3: Use All Operations

All 9 operations work the same with real AWS S3:
1. List all buckets
2. List objects in a bucket
3. Get object metadata
4. Download an object
5. Upload a file
6. Upload text content
7. Delete an object
8. Copy an object
9. Check if object exists
0. Exit

---

## Switching Between Local and AWS

### To Use Local (MinIO):
1. Make sure MinIO is running: `docker-compose up -d`
2. Use `appsettings.Local.json` or set `UseLocalEndpoint: true` in `appsettings.json`
3. Run: `dotnet run`

### To Use AWS:
1. Configure AWS credentials (see above)
2. Set `UseLocalEndpoint: false` in `appsettings.json`
3. Run: `dotnet run`

---

## Troubleshooting

### "Cannot connect to MinIO"
- Check Docker is running: `docker ps`
- Check MinIO is healthy: `curl http://localhost:9000/minio/health/live`
- Restart MinIO: `docker-compose restart`

### "Credentials not found"
- For local: Check `appsettings.Local.json` has `minioadmin` credentials
- For AWS: Run `aws configure` or check `~/.aws/credentials`

### "Bucket does not exist"
- For local: Run `./setup-test-data.sh` to create test buckets
- For AWS: Create bucket first using AWS Console or CLI

### Port 9000 or 9001 already in use
- Check what's using the port: `lsof -i :9000`
- Stop that service or edit `docker-compose.yml` to use different ports

---

## Next Steps

- Read [TESTING.md](TESTING.md) for detailed testing scenarios
- Read [README.md](README.md) for complete feature documentation
- Explore the S3Service.cs:1 class for programmatic usage
- Try all 9 operations to get familiar with the tool

## Quick Reference

| Operation | What it does |
|-----------|--------------|
| 1 | List all buckets |
| 2 | List objects in a bucket |
| 3 | Get object metadata |
| 4 | Download an object |
| 5 | Upload a file |
| 6 | Upload text content |
| 7 | Delete an object |
| 8 | Copy an object |
| 9 | Check if object exists |
| 0 | Exit |

Happy testing! 🎉
