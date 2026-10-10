namespace ServicePortal.Application.Interfaces;

public interface IFileStorageService
{
    long MaxFileSizeBytes { get; }
    IReadOnlyCollection<string> AllowedExtensions { get; }

    bool IsAllowedExtension(string fileName);
    bool IsAllowedSize(long sizeBytes);

    Task<(string StoredFileName, string ContentType, long SizeBytes)> SaveFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<(Stream FileStream, string ContentType)?> GetFileAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string storedFileName, CancellationToken cancellationToken = default);
}
