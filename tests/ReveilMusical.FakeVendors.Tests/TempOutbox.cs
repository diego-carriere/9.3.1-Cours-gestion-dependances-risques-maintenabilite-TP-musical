namespace ReveilMusical.FakeVendors.Tests;

/// <summary>Un dossier de sortie jetable par test : les SDK simulés y écrivent leurs journaux.</summary>
public sealed class TempOutbox : IDisposable
{
    public TempOutbox() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "reveil-outbox-" + Guid.NewGuid().ToString("N"));

    public string Read(string file)
    {
        var path = System.IO.Path.Combine(Path, file);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
