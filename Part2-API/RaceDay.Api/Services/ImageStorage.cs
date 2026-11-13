using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Saves an uploaded image somewhere public and gives back its URL.</summary>
public interface IImageStorage
{
    Task<string> SaveAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores images in Azure Blob Storage. The same code talks to a real storage account
/// or to the Azurite emulator, depending on BlobStorage:ConnectionString.
/// </summary>
public class AzureImageStorage : IImageStorage
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _containerReady;

    public AzureImageStorage(IConfiguration config)
    {
        var connectionString = config["BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("BlobStorage:ConnectionString is missing from appsettings.json.");
        var containerName = config["BlobStorage:ContainerName"] ?? "raceday-images";

        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        // A random name means two uploads never overwrite each other and the original file name is never trusted.
        var blob = _container.GetBlobClient($"{folder}/{Guid.NewGuid():N}{extension}");

        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return blob.Uri.ToString();
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerReady) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_containerReady) return;

            // Blob level public access lets the browser show the image straight from storage.
            await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
            _containerReady = true;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Checks an uploaded file before it goes anywhere near storage.</summary>
public static class ImageRules
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    /// <summary>Returns an error message, or null when the file is fine. The extension comes back through the out parameter.</summary>
    public static string? Validate(IFormFile? file, out string extension)
    {
        extension = string.Empty;

        if (file is null || file.Length == 0)
        {
            return "Choose an image file to upload.";
        }

        if (file.Length > MaxBytes)
        {
            return "The image must be 2 MB or smaller.";
        }

        if (!Extensions.TryGetValue(file.ContentType ?? string.Empty, out var found))
        {
            return "Only JPG, PNG, WEBP or GIF images are allowed.";
        }

        // The content type is just text the client sent, so look at the first bytes of the file too.
        using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = stream.Read(header, 0, header.Length);
        if (!LooksLikeImage(header, read))
        {
            return "That file is not a real image.";
        }

        extension = found;
        return null;
    }

    private static bool LooksLikeImage(byte[] h, int length)
    {
        if (length < 4) return false;

        var jpeg = h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
        var png = h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
        var gif = h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38;
        var webp = length >= 12
                   && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                   && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50;

        return jpeg || png || gif || webp;
    }
}
