namespace CoopMcpServer;

public interface ISaveDirectoryProvider
{
    string GetDirectory();
    string GetSessionDirectory();
}

public sealed class SaveDirectoryProvider : ISaveDirectoryProvider
{
    public string GetDirectory()
    {
        // FileDriver uses PlatformFileType.User; PlatformFileHelperPC resolves the redirected Personal folder.
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrEmpty(documents)) throw new IOException("Windows Documents known folder is unavailable.");
        return Path.Combine(documents, "Mount and Blade II Bannerlord", "Game Saves");
    }
    public string GetSessionDirectory()
    {
        string userDirectory = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
        return string.IsNullOrEmpty(userDirectory) ? GetDirectory() : Path.Combine(userDirectory, "Game Saves");
    }
}

public sealed record SaveEntry(string Name, long Bytes, DateTime LastWriteUtc, bool SessionSidecarPresent);
public sealed record SavePage(string Directory, SaveEntry[] Saves, int NextOffset, bool Truncated, string Note);

public interface ISaveCatalog
{
    SavePage List(int offset);
    SaveEntry ValidateSelection(string saveName);
}

public sealed class SaveCatalog : ISaveCatalog
{
    private const int MaximumEntries = 512;
    private const int PageSize = 64;
    private readonly ISaveDirectoryProvider directories;
    public SaveCatalog(ISaveDirectoryProvider directories) { this.directories = directories; }

    public SavePage List(int offset)
    {
        if (offset < 0 || offset > MaximumEntries) throw new ArgumentOutOfRangeException(nameof(offset));
        string directory = directories.GetDirectory();
        if (!Directory.Exists(directory)) return new SavePage(directory, Array.Empty<SaveEntry>(), 0, false, "Save directory does not exist; no saves were created.");
        var paths = Directory.EnumerateFiles(directory, "*.sav", SearchOption.TopDirectoryOnly).Take(MaximumEntries + 1).ToArray();
        var entries = paths.Take(MaximumEntries).Where(p => ValidName(Path.GetFileNameWithoutExtension(p)))
            .Select(p => new FileInfo(p)).Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0)
            .OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new SaveEntry(Path.GetFileNameWithoutExtension(f.Name), f.Length, f.LastWriteTimeUtc,
                File.Exists(Path.Combine(directories.GetSessionDirectory(), Path.GetFileNameWithoutExtension(f.Name) + ".json")))).ToArray();
        var page = entries.Skip(offset).Take(PageSize).ToArray();
        return new SavePage(directory, page, Math.Min(offset + page.Length, entries.Length), paths.Length > MaximumEntries,
            "Metadata only, not save compatibility/corruption validation. Missing .json means saved co-op registrations may not be restored. Listing may change between pages.");
    }

    public SaveEntry ValidateSelection(string saveName)
    {
        if (!ValidName(saveName)) throw new ArgumentException("save_name must be a catalog basename (1..128 characters), without paths or .sav/.json extension.", nameof(saveName));
        SaveEntry entry = null;
        int offset = 0;
        do
        {
            var page = List(offset);
            entry = page.Saves.SingleOrDefault(s => string.Equals(s.Name, saveName, StringComparison.Ordinal));
            if (entry != null || page.NextOffset == offset) break;
            offset = page.NextOffset;
        } while (offset < MaximumEntries);
        if (entry == null) throw new FileNotFoundException("Selected save is not in the bounded catalog; no launch or default-save fallback was attempted.", saveName);
        using var file = new FileStream(Path.Combine(directories.GetDirectory(), saveName + ".sav"), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length == 0) throw new IOException("Selected save is empty; no launch was attempted.");
        return entry;
    }

    private bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 128 && name == name.Trim() &&
        name != "." && name != ".." && !name.EndsWith('.') && !name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) &&
        !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !name.Any(char.IsControl) && !name.StartsWith('/');
}
