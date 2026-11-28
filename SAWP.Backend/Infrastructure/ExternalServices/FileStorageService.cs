using Core.Common;

namespace Infrastructure.ExternalServices;

public class FileStorageService : IFileStorageService
{
    private readonly string _baseStoragePath;

    public FileStorageService()
    {
        _baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, CancellationToken cancellationToken = default)
    {
        var safeFolder = UploadValidationHelper.SanitizeFileName(folderName, 64);
        var targetFolder = Path.Combine(_baseStoragePath, safeFolder);
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        var safeFileName = UploadValidationHelper.SanitizeFileName(fileName, 100);
        var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
        var fullPath = Path.Combine(targetFolder, uniqueFileName);

        using var destinationStream = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(destinationStream, cancellationToken);

        return Path.Combine(safeFolder, uniqueFileName);
    }

    private string? ResolveSafeFullPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || Path.IsPathRooted(filePath))
        {
            return null;
        }

        var rootFullPath = Path.GetFullPath(_baseStoragePath);
        if (!rootFullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            rootFullPath += Path.DirectorySeparatorChar;
        }

        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, filePath));
        if (!candidate.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return candidate;
    }

    public Task<Stream?> GetFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafeFullPath(filePath);
        if (fullPath == null || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public Task<bool> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafeFullPath(filePath);
        if (fullPath != null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public bool FileExists(string filePath)
    {
        var fullPath = ResolveSafeFullPath(filePath);
        return fullPath != null && File.Exists(fullPath);
    }
}
