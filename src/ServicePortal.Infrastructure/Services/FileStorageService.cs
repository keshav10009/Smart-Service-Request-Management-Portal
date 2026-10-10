using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;

namespace ServicePortal.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;
    private readonly ILogger<FileStorageService> _logger;

    private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".txt", ".png", ".jpg", ".jpeg", ".zip"
    };

    // 10 Megabytes limit
    public long MaxFileSizeBytes { get; } = 10 * 1024 * 1024;
    public IReadOnlyCollection<string> AllowedExtensions => _allowedExtensions;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _logger = logger;
        var configuredPath = configuration["FileStorage:Path"];

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            // Default storage outside public web root: App_Data/Uploads in application root
            var baseDir = AppContext.BaseDirectory;
            _storageDirectory = Path.Combine(baseDir, "App_Data", "Uploads");
        }
        else
        {
            _storageDirectory = configuredPath;
        }

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public bool IsAllowedExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && _allowedExtensions.Contains(ext);
    }

    public bool IsAllowedSize(long sizeBytes)
    {
        return sizeBytes > 0 && sizeBytes <= MaxFileSizeBytes;
    }

    public async Task<(string StoredFileName, string ContentType, long SizeBytes)> SaveFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowedExtension(originalFileName))
        {
            throw new InvalidOperationException($"File extension of '{originalFileName}' is not allowed.");
        }

        var ext = Path.GetExtension(originalFileName);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var destinationPath = Path.Combine(_storageDirectory, storedName);

        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await stream.CopyToAsync(fileStream, cancellationToken);

        var size = fileStream.Length;
        if (!IsAllowedSize(size))
        {
            // Clean up file if too large
            fileStream.Close();
            File.Delete(destinationPath);
            throw new InvalidOperationException($"File exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        _logger.LogInformation("Saved attachment {OriginalFileName} as {StoredFileName} ({SizeBytes} bytes) securely outside web root",
            originalFileName, storedName, size);

        return (storedName, contentType, size);
    }

    public Task<(Stream FileStream, string ContentType)?> GetFileAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        // Prevent directory traversal attacks
        var safeFileName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_storageDirectory, safeFileName);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<(Stream FileStream, string ContentType)?>(null);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = GetContentTypeForFileName(safeFileName);
        return Task.FromResult<(Stream FileStream, string ContentType)?>((stream, contentType));
    }

    public Task DeleteFileAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_storageDirectory, safeFileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private static string GetContentTypeForFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
