namespace ReveilMusical.Infrastructure.Tests;

public sealed class TempDirectory : IDisposable
{
    public TempDirectory() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "reveil-infra-" + Guid.NewGuid().ToString("N"));

    public string Read(string file)
    {
        var path = System.IO.Path.Combine(Path, file);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
