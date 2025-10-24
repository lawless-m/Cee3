#!/bin/bash

echo "=== Automated Test of Cee3 with Public S3 Bucket ==="
echo ""

# Test 1: List objects
echo "Test 1: Listing objects in ramsden-devstorage with prefix ProductImagesRamsden"
echo "2
ramsden-devstorage
ProductImagesRamsden
0" | dotnet run

echo ""
echo "=== Test Complete ==="
