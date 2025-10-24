# Parquet Metadata Export

Cee3 now supports exporting S3 object metadata to Apache Parquet files, enabling efficient data analysis and querying with tools like Python pandas, Apache Spark, DuckDB, and more.

## What is Parquet?

Apache Parquet is a columnar storage file format optimized for analytics. It provides:
- **Efficient compression** - Smaller file sizes
- **Fast querying** - Column-oriented access
- **Schema enforcement** - Strongly typed data
- **Wide compatibility** - Works with many data analysis tools

## Features

### Export Metadata (Operation 10)

Exports S3 object metadata to a Parquet file with the following schema:

| Column | Type | Description |
|--------|------|-------------|
| bucket_name | string | S3 bucket name |
| key | string | Object key/path |
| size | int64 | Object size in bytes |
| last_modified | timestamp | Last modification timestamp |
| etag | string | Object ETag (MD5 hash) |
| storage_class | string | S3 storage class (STANDARD, GLACIER, etc.) |
| content_type | string | MIME type of the object |
| owner | string | Object owner display name |
| is_latest | boolean | Whether this is the latest version |
| version_id | string | Object version ID (if versioning enabled) |

### Display Parquet Info (Operation 11)

Reads a Parquet file and displays:
- File statistics (row groups, schema)
- Column information
- First 10 records preview

## Usage

### Exporting Metadata

```
Select an operation (0-11): 10

Enter bucket name: my-bucket
Enter prefix (optional, press Enter to skip): documents/
Enter output Parquet file path (e.g., /tmp/metadata.parquet): /tmp/s3-metadata.parquet

Collecting metadata from bucket: my-bucket
Using prefix filter: documents/
Found 150 objects
Fetching detailed metadata...
  Processed 10 objects...
  Processed 20 objects...
  ...
Collected metadata for 150 objects
Writing to Parquet file: /tmp/s3-metadata.parquet
✓ Parquet file created successfully!
  File size: 12,345 bytes
  Records: 150
  Schema: 10 columns
```

### Viewing Parquet File Info

```
Select an operation (0-11): 11

Enter Parquet file path: /tmp/s3-metadata.parquet

Reading Parquet file: /tmp/s3-metadata.parquet

Parquet File Information:
  Row groups: 1
  Schema fields: 10

Schema:
  - bucket_name (DataField)
  - key (DataField)
  - size (DataField)
  - last_modified (DataField)
  - etag (DataField)
  - storage_class (DataField)
  - content_type (DataField)
  - owner (DataField)
  - is_latest (DataField)
  - version_id (DataField)

Total records: 150

First 10 records:
...
```

## Analyzing Parquet Files

### Using Python (pandas)

```python
import pandas as pd

# Read Parquet file
df = pd.read_parquet('/tmp/s3-metadata.parquet')

# Display basic info
print(df.info())
print(df.head())

# Analyze storage usage
total_size = df['size'].sum()
print(f"Total size: {total_size / (1024**3):.2f} GB")

# Group by storage class
storage_summary = df.groupby('storage_class')['size'].agg(['count', 'sum'])
print(storage_summary)

# Find largest files
largest = df.nlargest(10, 'size')[['key', 'size', 'last_modified']]
print(largest)

# Files by content type
content_types = df['content_type'].value_counts()
print(content_types)

# Recent files (last 7 days)
df['last_modified'] = pd.to_datetime(df['last_modified'])
recent = df[df['last_modified'] > pd.Timestamp.now() - pd.Timedelta(days=7)]
print(f"Files modified in last 7 days: {len(recent)}")
```

### Using DuckDB (SQL)

```sql
-- Install DuckDB and run queries
-- $ duckdb

-- Query the Parquet file directly
SELECT
    storage_class,
    COUNT(*) as file_count,
    SUM(size) / (1024*1024*1024) as total_gb,
    AVG(size) / (1024*1024) as avg_size_mb
FROM read_parquet('/tmp/s3-metadata.parquet')
GROUP BY storage_class
ORDER BY total_gb DESC;

-- Find large files
SELECT
    key,
    size / (1024*1024) as size_mb,
    last_modified,
    content_type
FROM read_parquet('/tmp/s3-metadata.parquet')
WHERE size > 100 * 1024 * 1024  -- Files > 100MB
ORDER BY size DESC
LIMIT 20;

-- Files by extension
SELECT
    regexp_extract(key, '\.([^.]+)$', 1) as extension,
    COUNT(*) as count,
    SUM(size) / (1024*1024) as total_mb
FROM read_parquet('/tmp/s3-metadata.parquet')
GROUP BY extension
ORDER BY count DESC;

-- Modified in the last month
SELECT
    DATE_TRUNC('day', last_modified) as date,
    COUNT(*) as files_modified
FROM read_parquet('/tmp/s3-metadata.parquet')
WHERE last_modified > CURRENT_DATE - INTERVAL '30 days'
GROUP BY date
ORDER BY date;
```

### Using Apache Spark

```python
from pyspark.sql import SparkSession

spark = SparkSession.builder.appName("S3Metadata").getOrCreate()

# Read Parquet file
df = spark.read.parquet('/tmp/s3-metadata.parquet')

# Register as temp table
df.createOrReplaceTempView("s3_metadata")

# Run SQL queries
spark.sql("""
    SELECT storage_class,
           COUNT(*) as count,
           SUM(size) as total_bytes
    FROM s3_metadata
    GROUP BY storage_class
""").show()

# Find duplicates by ETag
duplicates = spark.sql("""
    SELECT etag, COUNT(*) as count
    FROM s3_metadata
    GROUP BY etag
    HAVING count > 1
""")
duplicates.show()
```

## Use Cases

### Cost Analysis
- Calculate storage costs by storage class
- Identify candidates for lifecycle policies
- Find duplicate files (by ETag)
- Analyze storage growth over time

### Compliance & Auditing
- Track file modifications
- Verify file ownership
- Audit content types
- Check versioning status

### Performance Optimization
- Identify large files for caching
- Find frequently accessed patterns
- Optimize prefix structure
- Plan sharding strategies

### Data Migration
- Generate file inventory for migration
- Validate post-migration integrity
- Create transfer manifests
- Track migration progress

## Performance Considerations

### Large Buckets

For buckets with millions of objects:
1. Use prefix filtering to process in batches
2. Export to multiple Parquet files
3. Use partitioning by prefix or date
4. Consider pagination limits

Example batch processing:
```bash
# Export by prefix
dotnet run  # Then select 10, bucket: my-bucket, prefix: a/, output: metadata-a.parquet
dotnet run  # Then select 10, bucket: my-bucket, prefix: b/, output: metadata-b.parquet
# ... etc
```

### File Size Optimization

Parquet files are compressed efficiently:
- 1,000 objects ≈ 50 KB
- 10,000 objects ≈ 500 KB
- 100,000 objects ≈ 5 MB
- 1,000,000 objects ≈ 50 MB

## Integration Examples

### Scheduled Exports (Cron)

```bash
#!/bin/bash
# daily-export.sh

DATE=$(date +%Y-%m-%d)
OUTPUT="/data/s3-metadata-${DATE}.parquet"

# Use heredoc to automate input
cat << EOF | dotnet run --project /path/to/Cee3
10
my-production-bucket

${OUTPUT}
0
EOF

# Upload to analytics bucket
aws s3 cp ${OUTPUT} s3://analytics-bucket/metadata/date=${DATE}/
```

### CI/CD Pipeline

```yaml
# GitHub Actions example
name: Weekly S3 Inventory

on:
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday

jobs:
  inventory:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2

      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'

      - name: Export S3 Metadata
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          echo "10" | dotnet run
          # Process with your analytics
```

## Comparison with AWS Inventory

| Feature | Cee3 Parquet Export | AWS S3 Inventory |
|---------|---------------------|------------------|
| **Cost** | Free (only API calls) | $0.0025 per million objects |
| **Frequency** | On-demand, any time | Daily or weekly |
| **Latency** | Immediate | 24-48 hours |
| **Format** | Parquet | Parquet, CSV, ORC |
| **Filtering** | Prefix-based | Prefix-based |
| **Metadata** | Basic + custom | Comprehensive |
| **Setup** | None required | Requires configuration |

## Limitations

- Requires LIST permission on the bucket
- For very large buckets (millions of objects), consider AWS S3 Inventory
- Custom metadata fields are not included (only standard metadata)
- Requires read access to each object for full metadata

## Troubleshooting

### "Access Denied" on listing
- Ensure you have `s3:ListBucket` permission
- Check if bucket allows anonymous listing (for public buckets)
- Verify your AWS credentials are configured

### Out of memory for large exports
- Use prefix filtering to export in batches
- Increase available memory for the application
- Consider using AWS S3 Inventory for extremely large buckets

### Parquet file cannot be read
- Ensure you're using a compatible Parquet reader
- Check file permissions
- Verify the file was fully written (not interrupted)

## Additional Resources

- [Apache Parquet Documentation](https://parquet.apache.org/)
- [Parquet.Net GitHub](https://github.com/aloneguid/parquet-dotnet)
- [DuckDB Parquet Support](https://duckdb.org/docs/data/parquet)
- [Pandas read_parquet](https://pandas.pydata.org/docs/reference/api/pandas.read_parquet.html)
