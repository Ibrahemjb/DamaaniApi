namespace DammaniAPI.Services.Storage;

// Minimal storage seam (DMN-206 scope; first consumed by the public service
// request flow, DMN-504). Files are written under a NON-public root with
// random names — nothing under it is ever exposed via static file serving;
// downloads go through an authenticated endpoint (DMN-602).
public interface IFileStorage
{
    // Saves the content and returns the relative path to persist in the DB.
    Task<string> SaveAsync(Stream content, string subdirectory, string extension, CancellationToken ct);
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration)
        => _root = configuration["UPLOADS_ROOT"]
            ?? Path.Combine(AppContext.BaseDirectory, "uploads");

    public async Task<string> SaveAsync(Stream content, string subdirectory, string extension, CancellationToken ct)
    {
        var relativePath = $"{subdirectory}/{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);
        return relativePath;
    }
}
