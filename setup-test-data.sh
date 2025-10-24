#!/bin/bash

# Setup script for testing Cee3 with MinIO
# This script creates test buckets and sample files

echo "=== Cee3 Test Data Setup ==="
echo ""

# Check if docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running. Please start Docker first."
    exit 1
fi

# Start MinIO if not already running
echo "Starting MinIO..."
docker-compose up -d

echo "Waiting for MinIO to be ready..."
sleep 5

# Check if MinIO is accessible
max_attempts=30
attempt=0
while ! curl -s http://localhost:9000/minio/health/live > /dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ $attempt -ge $max_attempts ]; then
        echo "Error: MinIO failed to start after $max_attempts attempts"
        exit 1
    fi
    echo "  Waiting for MinIO... (attempt $attempt/$max_attempts)"
    sleep 2
done

echo "✓ MinIO is ready!"
echo ""

# Install MinIO client (mc) if not already installed
if ! command -v mc &> /dev/null; then
    echo "Installing MinIO client (mc)..."

    # Detect OS and install accordingly
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        wget -q https://dl.min.io/client/mc/release/linux-amd64/mc -O /tmp/mc
        chmod +x /tmp/mc
        MC_CMD="/tmp/mc"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        brew install minio/stable/mc 2>/dev/null || {
            wget -q https://dl.min.io/client/mc/release/darwin-amd64/mc -O /tmp/mc
            chmod +x /tmp/mc
            MC_CMD="/tmp/mc"
        }
    else
        echo "Warning: Could not install mc automatically. Skipping test data creation."
        echo "You can manually install mc from https://min.io/docs/minio/linux/reference/minio-mc.html"
        exit 0
    fi
else
    MC_CMD="mc"
fi

# Configure mc to connect to local MinIO
echo "Configuring MinIO client..."
$MC_CMD alias set local http://localhost:9000 minioadmin minioadmin > /dev/null 2>&1

# Create test buckets
echo "Creating test buckets..."
$MC_CMD mb local/test-bucket --ignore-existing > /dev/null 2>&1
$MC_CMD mb local/my-data-bucket --ignore-existing > /dev/null 2>&1
$MC_CMD mb local/backup-bucket --ignore-existing > /dev/null 2>&1

# Create test data directory
mkdir -p /tmp/cee3-test-data

# Create sample files
echo "Creating sample test files..."

cat > /tmp/cee3-test-data/sample.txt << EOF
Hello from Cee3!
This is a test file for S3 operations.
You can use this to test uploads, downloads, and metadata operations.
EOF

cat > /tmp/cee3-test-data/document.md << EOF
# Test Document

This is a markdown document for testing S3 operations.

## Features
- Upload and download
- Metadata management
- Copy operations
EOF

cat > /tmp/cee3-test-data/data.json << EOF
{
  "name": "Test Data",
  "type": "JSON",
  "purpose": "Testing S3 operations",
  "items": ["item1", "item2", "item3"]
}
EOF

# Upload test files
echo "Uploading test files to MinIO..."
$MC_CMD cp /tmp/cee3-test-data/sample.txt local/test-bucket/sample.txt > /dev/null 2>&1
$MC_CMD cp /tmp/cee3-test-data/document.md local/test-bucket/documents/readme.md > /dev/null 2>&1
$MC_CMD cp /tmp/cee3-test-data/data.json local/my-data-bucket/data/test.json > /dev/null 2>&1

# List created objects
echo ""
echo "=== Test Environment Ready! ==="
echo ""
echo "MinIO Console: http://localhost:9001"
echo "  Username: minioadmin"
echo "  Password: minioadmin"
echo ""
echo "Test Buckets Created:"
$MC_CMD ls local/ 2>/dev/null | awk '{print "  - " $NF}'
echo ""
echo "Sample Files Uploaded:"
echo "  - test-bucket/sample.txt"
echo "  - test-bucket/documents/readme.md"
echo "  - my-data-bucket/data/test.json"
echo ""
echo "To test with Cee3, run:"
echo "  dotnet run --environment Local"
echo ""
echo "Or use appsettings.Local.json configuration"
