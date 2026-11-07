using GameInterface.Services.Issues.Framework.Finalization;
using System.Collections.Generic;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class TextObjectComparerTests
{
    private const string Collected = "{=3957-test}Collected troops for {TARGET}";

    private readonly TextObjectComparer comparer = new();

    private static TextObject Text(string value, Dictionary<string, object> attributes = null) => new(value, attributes);

    private static TextObject Collect(object target) =>
        Text(Collected, new Dictionary<string, object> { ["TARGET"] = target, ["COUNT"] = 7 });

    [Fact]
    public void BothNull_AreSame()
    {
        Assert.True(comparer.SameText(null, null));
        Assert.True(comparer.SameKey(null, null));
    }

    [Fact]
    public void OneNull_IsNotSame()
    {
        Assert.False(comparer.SameText(Text("Danustica"), null));
        Assert.False(comparer.SameText(null, Text("Danustica")));
        Assert.False(comparer.SameKey(Text("Danustica"), null));
        Assert.False(comparer.SameKey(null, Text("Danustica")));
    }

    [Fact]
    public void EqualText_IsSame()
    {
        Assert.True(comparer.SameText(Collect("Danustica"), Collect("Danustica")));
        Assert.True(comparer.SameKey(Collect("Danustica"), Collect("Danustica")));
    }

    [Fact]
    public void DifferentValue_IsNotSame()
    {
        Assert.False(comparer.SameText(Text("{=a}Danustica"), Text("{=b}Danustica")));
        Assert.False(comparer.SameKey(Text("{=a}Danustica"), Text("{=b}Danustica")));
    }

    [Fact]
    public void SameValueWithDifferentAttribute_MatchesOnlyByKey()
    {
        Assert.False(comparer.SameText(Collect("Danustica"), Collect("Lageta")));
        Assert.True(comparer.SameKey(Collect("Danustica"), Collect("Lageta")));
    }

    [Fact]
    public void MissingOrExtraAttribute_MatchesOnlyByKey()
    {
        var fewer = Text(Collected, new Dictionary<string, object> { ["TARGET"] = "Danustica" });
        var renamed = Text(Collected, new Dictionary<string, object> { ["TARGET"] = "Danustica", ["AMOUNT"] = 7 });

        Assert.False(comparer.SameText(Collect("Danustica"), fewer));
        Assert.False(comparer.SameText(Collect("Danustica"), renamed));
        Assert.True(comparer.SameKey(Collect("Danustica"), fewer));
    }

    [Fact]
    public void NullAttributes_DifferFromEmptyAttributes()
    {
        Assert.False(comparer.SameText(Text("Danustica"), Text("Danustica", new Dictionary<string, object>())));
        Assert.True(comparer.SameKey(Text("Danustica"), Text("Danustica", new Dictionary<string, object>())));
    }

    [Fact]
    public void NestedText_IsComparedRecursively()
    {
        Assert.True(comparer.SameText(Collect(Text("{=town}Danustica")), Collect(Text("{=town}Danustica"))));
        Assert.False(comparer.SameText(Collect(Text("{=town}Danustica")), Collect(Text("{=town}Lageta"))));
        Assert.False(comparer.SameText(Collect(Text("{=town}Danustica")), Collect("{=town}Danustica")));
        Assert.True(comparer.SameKey(Collect(Text("{=town}Danustica")), Collect(Text("{=town}Lageta"))));
    }

    [Fact]
    public void NestedTextAttributes_AreCompared()
    {
        TextObject Nested(string name) =>
            Collect(Text("{=settlement}{NAME} castle", new Dictionary<string, object> { ["NAME"] = Text(name) }));

        Assert.True(comparer.SameText(Nested("Danustica"), Nested("Danustica")));
        Assert.False(comparer.SameText(Nested("Danustica"), Nested("Lageta")));
    }
}
