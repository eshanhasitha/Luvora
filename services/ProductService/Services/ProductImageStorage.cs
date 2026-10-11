using Microsoft.AspNetCore.Http;

namespace ProductService.Services;

public sealed class ProductImageStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly string _rootPath;

    public ProductImageStorage(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidImageException("Only JPG, PNG, WEBP, and GIF images are supported.");

        var contentType = file.ContentType.ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
            throw new InvalidImageException("The uploaded file must be a supported image.");

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destination = Path.Combine(_rootPath, fileName);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(output, cancellationToken);
        return $"/api/products/images/{fileName}";
    }

    public bool TryGetPath(string fileName, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            return false;

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, fileName));
        if (!candidate.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(candidate))
            return false;

        path = candidate;
        return true;
    }

    public static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}

public sealed class InvalidImageException(string message) : Exception(message);
