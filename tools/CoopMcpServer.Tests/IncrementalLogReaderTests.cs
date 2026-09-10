using System.Text;

namespace CoopMcpServer.Tests;

public sealed class IncrementalLogReaderTests : IDisposable
{
    private readonly string path = Path.GetTempFileName();
    private readonly IncrementalLogReader reader = new();

    [Fact]
    public void AppendsAdvanceCursorWithoutDuplicatesAndRespectUtf8Boundaries()
    {
        File.WriteAllText(path, "abc😀tail", new UTF8Encoding(false));
        var first = reader.Read(path, null, 4);
        Assert.Equal("abc", first.Text);
        var second = reader.Read(path, first.Cursor, 4);
        Assert.Equal("😀", second.Text);
        Assert.False(second.Reset);
        var third = reader.Read(path, second.Cursor, 100);
        Assert.Equal("tail", third.Text);
        File.AppendAllText(path, "new");
        var fourth = reader.Read(path, third.Cursor, 100);
        Assert.Equal("new", fourth.Text);
        Assert.False(fourth.Reset);
        Assert.False(fourth.HasMore);
    }

    [Fact]
    public void IncompleteTailFinishesDrainingAndResumesWhenCharacterIsCompleted()
    {
        File.WriteAllBytes(path, new byte[] { 0xF0, 0x9F });
        var first = reader.Read(path, null, 4);
        Assert.Empty(first.Text);
        Assert.False(first.HasMore);
        var second = reader.Read(path, first.Cursor, 4);
        Assert.Empty(second.Text);
        Assert.False(second.HasMore);
        Assert.Equal(first.Cursor, second.Cursor);
        using (var stream = new FileStream(path, FileMode.Append))
            stream.Write(new byte[] { 0x98, 0x80 });
        var completed = reader.Read(path, second.Cursor, 4);
        Assert.Equal("😀", completed.Text);
        Assert.False(completed.Reset);
        Assert.False(completed.HasMore);
    }

    [Fact]
    public void TruncationAndSameLengthRewriteResetCursor()
    {
        File.WriteAllText(path, "123456789");
        var first = reader.Read(path, null, 4);
        File.WriteAllText(path, "abcdefghi");
        var rewritten = reader.Read(path, first.Cursor, 100);
        Assert.True(rewritten.Reset);
        Assert.Equal("abcdefghi", rewritten.Text);
        File.WriteAllText(path, "x");
        var truncated = reader.Read(path, rewritten.Cursor, 100);
        Assert.True(truncated.Reset);
        Assert.Equal("x", truncated.Text);
    }

    [Fact]
    public void CompactionMarkerChangeResetsEvenWhenStartupPrefixIsPreservedAndFileRegrows()
    {
        string prefix = new string('a', 1024 * 1024);
        File.WriteAllText(path, prefix + "marker 1" + new string('b', 2000));
        var first = reader.Read(path, null, 4);
        File.WriteAllText(path, prefix + "marker 2" + new string('c', 3000));
        var next = reader.Read(path, first.Cursor, 4);
        Assert.True(next.Reset);
        Assert.Equal("aaaa", next.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65537)]
    public void OutputLimitIsEnforced(int maxBytes) => Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(path, null, maxBytes));

    [Fact]
    public void MalformedCursorIsRejected() => Assert.Throws<ArgumentException>(() => reader.Read(path, "not a cursor", 100));

    public void Dispose() => File.Delete(path);
}
