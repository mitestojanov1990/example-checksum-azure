
using Azure.Storage.Blobs;
using Serilog;
using SharpCompress.Archives.Tar;
using SharpCompress.Compressors.BZip2;
using System.Security.Cryptography;

public class ChecksumService : BackgroundService, IChecksumService
{
    private readonly BlobServiceClient _blobServiceClient;
    private string _fileName;
    private string _checksumFileName;
    private string _fileListingFileName;
    private string _containerName;
    private readonly TaskCompletionSource _initializationComplete = new TaskCompletionSource();

    public ChecksumService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public void Initialize(string fileName, string checksumFileName, string fileListingFileName, string containerName)
    {
        _fileName = fileName;
        _checksumFileName = checksumFileName;
        _fileListingFileName = fileListingFileName;
        _containerName = containerName;
        _initializationComplete.SetResult();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _initializationComplete.Task;
        var tempFilePath = await DownloadBlobToFileAsync(_fileName);
        try
        {
            var computedChecksum = ComputeChecksum(tempFilePath);
            await ValidateChecksum(computedChecksum);

            if (!String.IsNullOrEmpty(_fileListingFileName))
            {
                await ValidateFileListing(tempFilePath);
            }
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    private async Task<string> DownloadBlobToFileAsync(string blobName)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(blobName);
        var tempFilePath = Path.GetTempFileName();
        await using var tempFile = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 4096, useAsync: true);
        await blobClient.DownloadToAsync(tempFile);
        return tempFilePath;
    }

    private string ComputeChecksum(string filePath)
    {
        using var sha1 = SHA1.Create();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        // Compute the hash in chunks to handle large files
        byte[] hash;
        const int bufferSize = 4 * 1024 * 1024;  // 4 MB buffer
        byte[] buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = fileStream.Read(buffer, 0, bufferSize)) > 0)
        {
            sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
        }
        sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        hash = sha1.Hash;

        return BitConverter.ToString(hash).Replace("-", "");
    }
    private async Task ValidateChecksum(string computedChecksum)
    {
        var checksumBlobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(_checksumFileName);
        var expectedChecksum = await checksumBlobClient.DownloadContentAsync();
        var isChecksumValid = computedChecksum.Equals(expectedChecksum.Value.Content.ToString(), StringComparison.OrdinalIgnoreCase);

        var resultBlobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient($"{Guid.NewGuid()}_{_fileName}_result.txt");
        var resultContent = new BinaryData($"Checksum validation result: {isChecksumValid}");
        await resultBlobClient.UploadAsync(resultContent.ToStream());

        Log.Information($"Checksum validation for {_fileName} succeeded: {isChecksumValid}");
    }

    private async Task ValidateFileListing(string tempFilePath)
    {
        using var tempFile = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
        using var bzip2Stream = new BZip2Stream(tempFile, SharpCompress.Compressors.CompressionMode.Decompress, decompressConcatenated: false);
        using var tarArchive = TarArchive.Open(bzip2Stream);

        var fileListing = string.Empty;
        foreach (var entry in tarArchive.Entries)
        {
            if (!entry.IsDirectory)
            {
                fileListing += entry.Key + "\n";
            }
        }

        var fileListingBlobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(_fileListingFileName);
        var expectedFileListingContent = await fileListingBlobClient.DownloadContentAsync();
        var expectedFileListing = expectedFileListingContent.Value.Content.ToString();

        var isFileListingValid = fileListing.Equals(expectedFileListing);

        Log.Information($"File listing validation for {_fileName} succeeded: {isFileListingValid}");

    }
}