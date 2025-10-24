#!/bin/bash

# Test script for public S3 bucket access
echo "Testing Cee3 with public S3 bucket (ramsden-devstorage)..."
echo ""

# Test listing objects with prefix using AWS CLI as a baseline
echo "=== Testing with AWS CLI first (baseline) ==="
aws s3 ls s3://ramsden-devstorage/ProductImagesRamsden/ --region eu-west-2 --no-sign-request --recursive | head -20

echo ""
echo "=== Now testing with Cee3 ==="
echo ""
echo "Run: dotnet run"
echo "Then select option 2 (List objects in a bucket)"
echo "Bucket name: ramsden-devstorage"
echo "Prefix: ProductImagesRamsden"
