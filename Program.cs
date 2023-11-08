using Azure.Storage.Blobs;
using System.Security.Cryptography;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("StorageAccount");

builder.Services.AddSingleton<BlobServiceClient>(provider =>
    new Azure.Storage.Blobs.BlobServiceClient(connectionString)
);
builder.Services.AddSingleton<IChecksumService, ChecksumService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<IChecksumService>() as ChecksumService);
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<IUploadService>() as UploadService);
try
{

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .WriteTo.AzureTableStorage(
            connectionString,
            storageTableName: "logs"
        ).CreateLogger();

}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    throw;
}
var app = builder.Build();
app.MapPost("/upload", async (HttpContext context, IUploadService uploadService) =>
{
    Log.Information("Upload called.");

    var req = context.Request;

    if (req.Form.Files.Count == 0)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Log.Information("No files received from the upload");
        await context.Response.WriteAsync("No files received from the upload");
        return;
    }

    if (req.HasFormContentType)
    {
        var file = req.Form.Files[0];
        var form = await req.ReadFormAsync();
        var containerName = form["containername"];

        // Copy the uploaded file to a temporary file
        var tempFilePath = Path.GetTempFileName();
        await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(fileStream);
        }

        // Pass the path of the temporary file to the background service
        uploadService.Initialize(tempFilePath, file.FileName, containerName);
        await context.Response.WriteAsync("Upload started in background.");
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Log.Information("Request content type is not supported.");
        await context.Response.WriteAsync("Request content type is not supported.");
    }
});

app.MapPost("/validatechecksumbg", async (HttpContext context, IChecksumService checksumService) =>
{
    Log.Information($"Checksum validation called.");
    var req = context.Request;
    if (req.HasFormContentType)
    {
        var form = await req.ReadFormAsync();
        var fileName = form["filename"];
        var checksumFileName = form["checksumfilename"];
        var fileListingName = form["filelistingname"];
        var containerName = form["containername"];

        Log.Information($"Checksum validation started for {fileName}");
        checksumService.Initialize(fileName, checksumFileName, fileListingName, containerName);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Log.Information($"Request content type is not supported.");
        await context.Response.WriteAsync("Request content type is not supported.");
    }
});

app.Run();
