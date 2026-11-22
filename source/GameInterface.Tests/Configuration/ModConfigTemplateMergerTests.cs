using GameInterface.Configuration;
using Xunit;

namespace GameInterface.Tests.Configuration;

public class ModConfigTemplateMergerTests
{
    private const string Template = @"{
  ""block"": {
    // First setting.
    ""first"": 1,
    ""second"": 2,
    ""third"": 3
  }
}";

    [Theory]
    // At the top of a block, set apart from the next key because the new key has a comment.
    [InlineData(@"{
  ""block"": {
    ""second"": 2,
    ""third"": 3
  }
}", @"{
  ""block"": {
    // First setting.
    ""first"": 1,

    ""second"": 2,
    ""third"": 3
  }
}", "block.first")]
    // Two new keys in a row.
    [InlineData(@"{
  ""block"": {
    ""third"": 3
  }
}", @"{
  ""block"": {
    // First setting.
    ""first"": 1,
    ""second"": 2,
    ""third"": 3
  }
}", "block.first, block.second")]
    // After a last value with a trailing comment, which gets its comma in front of the comment.
    [InlineData(@"{
  ""block"": {
    ""first"": 1,
    ""second"": 2 // note
  }
}", @"{
  ""block"": {
    ""first"": 1,
    ""second"": 2, // note
    ""third"": 3
  }
}", "block.third")]
    // After a value followed by another key on the same line.
    [InlineData(@"{ ""block"": { ""first"": 1, ""third"": 3 } }", @"{ ""block"": { ""first"": 1,
    ""second"": 2, ""third"": 3 } }", "block.second")]
    public void Merge_InsertsNewKeysAtEachPosition(string config, string expected, string added)
    {
        var (merged, addedSettings) = ModConfigTemplateMerger.Merge(Lf(config), Lf(Template));

        Assert.Equal(Lf(expected), merged);
        Assert.Equal(added, string.Join(", ", addedSettings));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n");
}
