
using Azure.Storage.Blobs;
using System.Security.Cryptography;

public class UploadService : BackgroundService, IUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private string _tempFilePath;
    private string _fileName;
    private string _containerName;
    private readonly TaskCompletionSource _initializationComplete = new TaskCompletionSource();

    public UploadService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public void Initialize(string tempFilePath, string fileName, string containerName)
    {
        _tempFilePath = tempFilePath;
        _fileName= fileName;
        _containerName = containerName;
        _initializationComplete.SetResult();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _initializationComplete.Task;
        try
        {
            await UploadFileAndChecksumAsync();
        }
        finally
        {
            // Delete the temporary file if it exists
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }
    }
    public async Task UploadFileAndChecksumAsync()
    {
        // Assume _tempFilePath is a string member variable that holds the path to the temp file.

        // Ensure container exists.
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        // Upload the file to blob storage.
        var blobClient = containerClient.GetBlobClient(_fileName);
        using (var uploadFileStream = File.OpenRead(_tempFilePath))
        {
            await blobClient.UploadAsync(uploadFileStream, overwrite: true);
        }

        // Compute the SHA1 checksum.
        string checksum;
        using (var sha1 = SHA1.Create())
        {
            using var checksumStream = File.OpenRead(_tempFilePath);
            var hash = sha1.ComputeHash(checksumStream);
            checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // Upload the checksum to a new blob.
        var checksumBlob = containerClient.GetBlobClient(_fileName + ".sha1");
        await checksumBlob.UploadAsync(new BinaryData(checksum), overwrite: true);

        // Clean up the temporary file.
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

}