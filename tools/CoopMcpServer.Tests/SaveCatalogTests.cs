namespace CoopMcpServer.Tests;

public sealed class SaveCatalogTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CoopSaveCatalog-" + Guid.NewGuid().ToString("N"));
    private readonly SaveCatalog catalog;
    private sealed class DirectoryProvider(string path) : ISaveDirectoryProvider
    {
        public string GetDirectory() => path;
        public string GetSessionDirectory() => path;
    }
    public SaveCatalogTests()
    {
        Directory.CreateDirectory(directory);
        catalog = new SaveCatalog(new DirectoryProvider(directory));
    }
    [Fact]
    public void ListsAndSelectsNamesWithSpacesWithoutChangingSaveBytes()
    {
        string save = Path.Combine(directory, "Danustica campaign.sav");
        byte[] bytes = { 5, 9, 1, 4 };
        File.WriteAllBytes(save, bytes);
        File.WriteAllText(Path.Combine(directory, "Danustica campaign.json"), "sidecar is not parsed");
        var entry = Assert.Single(catalog.List(0).Saves);
        Assert.Equal("Danustica campaign", entry.Name);
        Assert.Equal(4, entry.Bytes);
        Assert.True(entry.SessionSidecarPresent);
        Assert.Equal(entry, catalog.ValidateSelection(entry.Name));
        Assert.Equal(bytes, File.ReadAllBytes(save));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }
    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("C:\\outside")]
    [InlineData("save.sav")]
    [InlineData("save.json")]
    [InlineData(" leading")]
    [InlineData("ending.")]
    [InlineData("/server")]
    [InlineData("")]
    public void RejectsPathsExtensionsAndFlags(string name) => Assert.Throws<ArgumentException>(() => catalog.ValidateSelection(name));
    [Fact]
    public void MissingEmptyAndLockedSavesFailBeforeLaunch()
    {
        Assert.Throws<FileNotFoundException>(() => catalog.ValidateSelection("missing"));
        string path = Path.Combine(directory, "empty.sav");
        File.WriteAllBytes(path, Array.Empty<byte>());
        Assert.Throws<IOException>(() => catalog.ValidateSelection("empty"));
        File.WriteAllText(path, "not empty");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => catalog.ValidateSelection("empty"));
    }
    [Fact]
    public void CatalogIsPagedAndBoundedWithoutReadingSaveContent()
    {
        for (int i = 0; i < 514; i++) File.WriteAllText(Path.Combine(directory, "save" + i.ToString("D4") + ".sav"), "opaque");
        var page = catalog.List(0);
        Assert.Equal(64, page.Saves.Length);
        Assert.Equal(64, page.NextOffset);
        Assert.True(page.Truncated);
        Assert.False(page.Saves[0].SessionSidecarPresent);
        Assert.Empty(catalog.List(512).Saves);
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.List(513));
    }
    public void Dispose() => Directory.Delete(directory, true);
}
