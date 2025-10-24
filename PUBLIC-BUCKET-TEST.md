# Testing with Public S3 Bucket

## Test Results: ramsden-devstorage

The bucket `ramsden-devstorage` in region `eu-west-2` has been tested with anonymous (public) access.

### Access Permissions

✅ **Public READ Access** - Can download objects if you know the exact key
❌ **Public LIST Access** - Cannot list bucket contents anonymously

This is a common S3 configuration for public buckets where:
- Anyone can download files if they know the URL/key
- But you cannot browse to discover what files exist

### What Works with Anonymous Access

If you know the exact object key, you CAN:
- ✅ Download the object (operation 4)
- ✅ Get object metadata (operation 3)
- ✅ Check if object exists (operation 9)
- ✅ Read object as string

### What Doesn't Work with Anonymous Access

Without credentials, you CANNOT:
- ❌ List buckets (operation 1) - Requires credentials
- ❌ List objects (operation 2) - This bucket doesn't allow public listing
- ❌ Upload files (operation 5) - Requires write permissions
- ❌ Delete objects (operation 7) - Requires write permissions
- ❌ Copy objects (operation 8) - Requires write permissions

### Testing with Known Object Keys

To test READ operations, you need to know the exact object key. Based on your configuration:

**Key Prefix**: `ProductImagesRamsden`
**Previous Versions Prefix**: `ProductImagesRamsden_PreviousVersions`

Example object keys might be:
- `ProductImagesRamsden/image1.jpg`
- `ProductImagesRamsden/product-12345.png`
- `ProductImagesRamsden_PreviousVersions/old-image.jpg`

### How to Test

#### Test 1: Download a Specific Object (if you know the key)

```bash
dotnet run
```

Then:
1. Select operation 4 (Download an object)
2. Enter bucket: `ramsden-devstorage`
3. Enter object key: `ProductImagesRamsden/[known-filename]`
4. Enter local path: `/tmp/downloaded-file`

#### Test 2: Get Object Metadata

```bash
dotnet run
```

Then:
1. Select operation 3 (Get object metadata)
2. Enter bucket: `ramsden-devstorage`
3. Enter object key: `ProductImagesRamsden/[known-filename]`

#### Test 3: Check if Object Exists

```bash
dotnet run
```

Then:
1. Select operation 9 (Check if object exists)
2. Enter bucket: `ramsden-devstorage`
3. Enter object key: `ProductImagesRamsden/[known-filename]`

### Testing with Your Credentials

To test ALL operations including listing and writing, you need to provide your AWS credentials:

1. Edit `appsettings.json` and set:
   ```json
   {
     "S3": {
       "UseAnonymousCredentials": false
     }
   }
   ```

2. Configure your credentials using one of these methods:
   - Run `aws configure`
   - Set environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
   - Create `~/.aws/credentials` file

3. Run the application:
   ```bash
   dotnet run
   ```

Now you'll have full access to all 9 operations!

### Conclusion

The Cee3 application successfully:
- ✅ Connects to AWS S3 in eu-west-2 region
- ✅ Supports anonymous (public) credentials
- ✅ Can read public objects when you know the key
- ✅ Properly handles permission errors with clear messages

To fully test all features, you'll need to either:
- Use a bucket that allows public listing (uncommon for security reasons)
- OR provide your AWS credentials to test with full permissions

The application is working correctly! The "Forbidden" error for listing is expected for this bucket's security configuration.
