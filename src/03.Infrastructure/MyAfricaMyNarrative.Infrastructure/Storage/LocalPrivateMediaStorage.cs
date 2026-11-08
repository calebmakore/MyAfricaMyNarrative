using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Infrastructure.Storage;

public sealed class LocalPrivateMediaStorage : IPrivateMediaStorage
{
    private readonly string _root;

    public LocalPrivateMediaStorage(IOptions<MediaOptions> options)
    {
        _root = Path.GetFullPath(options.Value.StorageRoot);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredMedia> WriteOriginalAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        var key = $"{Guid.NewGuid():N}.original";
        var path = Resolve(key);
        var temporaryPath = path + ".uploading";
        long length = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                length += read;
                if (length > maxBytes) throw new InvalidDataException($"Upload exceeds the {maxBytes} byte limit.");
                hash.AppendData(buffer.AsSpan(0, read));
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await destination.FlushAsync(cancellationToken);
            File.Move(temporaryPath, path);
            return new StoredMedia(key, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new FileStream(Resolve(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.Contains(Path.DirectorySeparatorChar) || key.Contains(Path.AltDirectorySeparatorChar))
            throw new InvalidDataException("Invalid private media key.");
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Private media path escaped its root.");
        return path;
    }
}
