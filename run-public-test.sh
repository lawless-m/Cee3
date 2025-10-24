#!/bin/bash

echo "Compiling public bucket test..."
dotnet build

echo ""
echo "Running public bucket test..."
echo ""

# Temporarily rename Program.cs and use TestPublicBucket.cs as entry point
mv Program.cs Program.cs.bak
mv TestPublicBucket.cs TestPublicBucket.cs.bak
cp TestPublicBucket.cs.bak Program.cs

dotnet run

# Restore original files
mv Program.cs TestPublicBucket.cs
mv Program.cs.bak Program.cs
mv TestPublicBucket.cs.bak TestPublicBucket.cs.bak.original 2>/dev/null || true

echo ""
echo "Test complete!"
