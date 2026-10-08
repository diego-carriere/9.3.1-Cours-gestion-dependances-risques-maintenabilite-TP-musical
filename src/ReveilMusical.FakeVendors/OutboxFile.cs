namespace ReveilMusical.FakeVendors;

/// <summary>
/// La « sortie » des SDK simulés : une ligne par envoi, dans un fichier du dossier configuré, et
/// sur la console si demandé. Aucun envoi réel (exigence du brief).
/// </summary>
internal sealed class OutboxFile
{
    private readonly Lock _gate = new();
    private readonly string _path;
    private readonly bool _writeToConsole;

    public OutboxFile(string directory, string fileName, bool writeToConsole)
    {
        _path = Path.Combine(directory, fileName);
        _writeToConsole = writeToConsole;
    }

    public void Append(string line)
    {
        var stamped = $"{DateTimeOffset.UtcNow:O} {line}";

        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.AppendAllText(_path, stamped + Environment.NewLine);
        }

        if (_writeToConsole)
        {
            Console.Out.WriteLine(stamped);
        }
    }
}
