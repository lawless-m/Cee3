# Local Testing Guide

This guide explains how to test Cee3 without requiring AWS credentials, using a local S3-compatible server (MinIO).

## Why Test Locally?

- **No AWS credentials needed** - Test without AWS account setup
- **Fast and free** - No AWS costs or network latency
- **Safe testing** - Experiment without affecting production data
- **Offline development** - Work without internet connection

## Prerequisites

- Docker and Docker Compose
- .NET 8.0 SDK

## Quick Start

### 1. Start MinIO and Setup Test Data

Run the automated setup script:

```bash
./setup-test-data.sh
```

This script will:
- Start MinIO in Docker
- Create test buckets (test-bucket, my-data-bucket, backup-bucket)
- Upload sample files for testing
- Configure the MinIO client

### 2. Run Cee3 in Local Mode

Option A - Using appsettings.Local.json (recommended):
```bash
dotnet run
# Then manually copy appsettings.Local.json over appsettings.json
# Or use the environment variable approach below
```

Option B - Using environment variable:
```bash
ASPNETCORE_ENVIRONMENT=Local dotnet run
```

Option C - Manually edit appsettings.json:
```json
{
  "S3": {
    "UseLocalEndpoint": true,
    "LocalEndpoint": "http://localhost:9000",
    "LocalAccessKey": "minioadmin",
    "LocalSecretKey": "minioadmin"
  }
}
```

### 3. Access MinIO Console (Optional)

Open your browser to: http://localhost:9001

- **Username**: minioadmin
- **Password**: minioadmin

This web interface allows you to:
- Browse buckets and objects
- Upload/download files directly
- Manage access policies
- Monitor operations

## Manual Setup (Alternative)

If you prefer to set up manually or the script doesn't work on your system:

### 1. Start MinIO

```bash
docker-compose up -d
```

### 2. Verify MinIO is Running

```bash
curl http://localhost:9000/minio/health/live
```

You should see "200 OK".

### 3. Create a Test Bucket

Using the MinIO Console (http://localhost:9001):
1. Login with minioadmin/minioadmin
2. Click "Create Bucket"
3. Name it "test-bucket"
4. Click "Create"

Or using the AWS CLI (configured for MinIO):
```bash
aws --endpoint-url http://localhost:9000 s3 mb s3://test-bucket
```

### 4. Run Cee3

Make sure `appsettings.Local.json` exists with:
```json
{
  "S3": {
    "UseLocalEndpoint": true,
    "LocalEndpoint": "http://localhost:9000",
    "LocalAccessKey": "minioadmin",
    "LocalSecretKey": "minioadmin"
  }
}
```

Then run:
```bash
dotnet run
```

## Testing Scenarios

Once Cee3 is running in local mode, try these operations:

### 1. List Buckets
```
Select operation: 1
```
Should show: test-bucket, my-data-bucket, backup-bucket (if setup script was used)

### 2. List Objects
```
Select operation: 2
Enter bucket name: test-bucket
```
Should show any files you've uploaded

### 3. Upload a File
```
Select operation: 5
Enter local file path: README.md
Enter destination bucket: test-bucket
Enter destination key: uploads/readme.md
Add custom metadata? n
```

### 4. Get Object Metadata
```
Select operation: 3
Enter bucket name: test-bucket
Enter object key: sample.txt
```
Should display file metadata including size, content type, etc.

### 5. Download a File
```
Select operation: 4
Enter bucket name: test-bucket
Enter object key: sample.txt
Enter local file path: /tmp/downloaded-sample.txt
```

### 6. Upload with Custom Metadata
```
Select operation: 5
Enter local file path: LICENSE
Enter destination bucket: test-bucket
Enter destination key: docs/license.txt
Add custom metadata? y
  Metadata key: author
  Value for 'author': Your Name
  Metadata key: project
  Value for 'project': Cee3
  Metadata key: (press Enter)
```

### 7. Copy an Object
```
Select operation: 8
Enter source bucket: test-bucket
Enter source object key: sample.txt
Enter destination bucket: backup-bucket
Enter destination key: backups/sample-backup.txt
```

## Stopping MinIO

When you're done testing:

```bash
docker-compose down
```

To also remove the data volumes:

```bash
docker-compose down -v
```

## Troubleshooting

### MinIO won't start

**Check if port is already in use:**
```bash
lsof -i :9000
lsof -i :9001
```

**Solution:** Stop the service using those ports or change the ports in docker-compose.yml

### Connection refused errors

**Check MinIO is running:**
```bash
docker ps | grep minio
```

**Check health:**
```bash
curl http://localhost:9000/minio/health/live
```

**Solution:** Restart MinIO:
```bash
docker-compose restart
```

### "Bucket does not exist" error

**Create the bucket first:**
- Use MinIO Console: http://localhost:9001
- Or use operation "1" to see existing buckets, then create one via the console

### Access Denied errors

**Check credentials in appsettings.Local.json:**
```json
{
  "S3": {
    "LocalAccessKey": "minioadmin",
    "LocalSecretKey": "minioadmin"
  }
}
```

### ForcePathStyle errors

The application automatically sets `ForcePathStyle = true` for local endpoints. If you still see errors, verify your MinIO configuration.

## Switching Between Local and AWS

To switch back to AWS S3:

1. Edit `appsettings.json`:
   ```json
   {
     "S3": {
       "UseLocalEndpoint": false
     }
   }
   ```

2. Or delete/rename `appsettings.Local.json`

3. Or use a different environment:
   ```bash
   ASPNETCORE_ENVIRONMENT=Production dotnet run
   ```

## Differences Between MinIO and AWS S3

MinIO aims for S3 compatibility, but there are minor differences:

### Fully Compatible:
- ✓ Basic operations (list, get, put, delete)
- ✓ Metadata
- ✓ Multipart uploads
- ✓ Bucket operations
- ✓ Object copying

### Minor Differences:
- Storage classes (MinIO uses STANDARD, REDUCED_REDUNDANCY)
- Some advanced AWS-specific features (Glacier, Intelligent-Tiering)
- Regional endpoints (MinIO is single-instance)
- IAM policies (MinIO has its own policy system)

For basic S3 operations like those in Cee3, MinIO is fully compatible.

## Performance Testing

MinIO is great for load testing since it runs locally:

```bash
# Test upload performance
time for i in {1..100}; do echo "test $i" | aws --endpoint-url http://localhost:9000 s3 cp - s3://test-bucket/test-$i.txt; done

# Test download performance
time for i in {1..100}; do aws --endpoint-url http://localhost:9000 s3 cp s3://test-bucket/test-$i.txt /tmp/test-$i.txt; done
```

## Integration Testing

You can use MinIO in your CI/CD pipeline:

```yaml
# Example GitHub Actions workflow
services:
  minio:
    image: minio/minio
    ports:
      - 9000:9000
    env:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    options: --health-cmd "curl -f http://localhost:9000/minio/health/live"
```

## Additional Resources

- [MinIO Documentation](https://min.io/docs/minio/)
- [MinIO Docker Hub](https://hub.docker.com/r/minio/minio)
- [AWS S3 API Reference](https://docs.aws.amazon.com/AmazonS3/latest/API/)
- [MinIO Client (mc) Guide](https://min.io/docs/minio/linux/reference/minio-mc.html)
