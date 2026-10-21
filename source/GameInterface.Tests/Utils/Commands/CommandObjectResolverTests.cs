using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

// Covers the id-then-name matching core of CommandObjectResolver with fake candidates.
public class CommandObjectResolverTests
{
    private sealed class Fake
    {
        public Fake(string registryId, string stringId, string name)
        {
            RegistryId = registryId;
            StringId = stringId;
            Name = name;
        }

        public string RegistryId { get; }
        public string StringId { get; }
        public string Name { get; }
    }

    // Runs the core with registry ids and string ids taken from the fakes, like the real resolver does.
    private static bool Match(
        string? query,
        IReadOnlyList<Fake> candidates,
        Dictionary<string, Fake> registry,
        bool mustBeCandidate,
        out Fake value,
        out string error)
    {
        return CommandObjectResolver.TryMatch(
            "Fake",
            query,
            candidates,
            id => registry.TryGetValue(id, out var hit) ? hit : null,
            mustBeCandidate,
            fake => new[] { fake.RegistryId, fake.StringId },
            fake => fake.Name,
            fake => fake.RegistryId ?? fake.StringId,
            out value,
            out error);
    }

    private static bool Match(string? query, IReadOnlyList<Fake> candidates, out Fake value, out string error) =>
        Match(query, candidates, new Dictionary<string, Fake>(), false, out value, out error);

    [Fact]
    public void RegistryIdHit_ResolvesWithoutNameMatch()
    {
        var dead = new Fake("hero_dead", "dead_string", "Gone");
        var registry = new Dictionary<string, Fake> { ["hero_dead"] = dead };

        var ok = Match("hero_dead", new List<Fake>(), registry, false, out var value, out var error);

        Assert.True(ok);
        Assert.Same(dead, value);
        Assert.Null(error);
    }

    [Fact]
    public void StringIdMatch_ResolvesWhenRegistryMisses()
    {
        var target = new Fake("reg_1", "string_1", "Alpha");
        var other = new Fake("reg_2", "string_2", "Beta");

        var ok = Match("string_1", new[] { target, other }, out var value, out _);

        Assert.True(ok);
        Assert.Same(target, value);
    }

    [Fact]
    public void IdMatch_IsCaseSensitive()
    {
        var target = new Fake("reg_1", "String_1", "Alpha");

        var ok = Match("string_1", new[] { target }, out _, out var error);

        Assert.False(ok);
        Assert.Equal("No Fake found with id or name 'string_1'.", error);
    }

    [Fact]
    public void UniqueName_MatchesCaseInsensitivelyAndTrimmed()
    {
        var target = new Fake("reg_1", "string_1", " Danustica ");
        var other = new Fake("reg_2", "string_2", "Zeonica");

        var ok = Match("  danUSTica ", new[] { target, other }, out var value, out _);

        Assert.True(ok);
        Assert.Same(target, value);
    }

    [Fact]
    public void IdWins_WhenAnotherCandidateHasThatNameText()
    {
        var byId = new Fake("reg_1", "alpha", "Something");
        var byName = new Fake("reg_2", "other", "alpha");

        var ok = Match("alpha", new[] { byName, byId }, out var value, out _);

        Assert.True(ok);
        Assert.Same(byId, value);
    }

    [Fact]
    public void AmbiguousName_ListsCandidatesWithIdsOrderedById()
    {
        var b = new Fake("reg_b", "s_b", "Twin");
        var a = new Fake("reg_a", "s_a", "twin");

        var ok = Match("TWIN", new[] { b, a, new Fake("reg_c", "s_c", "Other") }, out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("Several Fakes are named 'TWIN'; use an id. Candidates: twin (reg_a), Twin (reg_b)", error);
    }

    [Fact]
    public void AmbiguousName_PrintedIdIsAcceptedOnRerun()
    {
        var a = new Fake("reg_a", "s_a", "Twin");
        var b = new Fake("reg_b", "s_b", "Twin");
        var candidates = new[] { a, b };

        Match("Twin", candidates, out _, out _);
        var ok = Match("reg_b", candidates, out var value, out _);

        Assert.True(ok);
        Assert.Same(b, value);
    }

    [Fact]
    public void AmbiguousName_CapsListAtTwentyFive()
    {
        var candidates = Enumerable.Range(0, 30)
            .Select(i => new Fake($"reg_{i:00}", $"s_{i:00}", "Same"))
            .ToList();

        var ok = Match("Same", candidates, out _, out var error);

        Assert.False(ok);
        Assert.Contains("reg_24", error);
        Assert.DoesNotContain("reg_25", error);
        Assert.EndsWith(", ... and 5 more", error);
    }

    [Fact]
    public void NoMatch_ReportsIdOrNameMessage()
    {
        var ok = Match("Nobody", new[] { new Fake("reg_1", "s_1", "Alpha") }, out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("No Fake found with id or name 'Nobody'.", error);
    }

    [Fact]
    public void NullOrEmptyCandidateNames_AreSkipped()
    {
        var unnamed = new Fake("reg_1", "s_1", null);
        var blank = new Fake("reg_2", "s_2", "   ");

        var named = new Fake("reg_3", "s_3", "Alpha");

        var ok = Match("Alpha", new[] { unnamed, blank, named }, out var value, out _);

        Assert.True(ok);
        Assert.Same(named, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQuery_Fails(string? query)
    {
        var ok = Match(query, new[] { new Fake("reg_1", "s_1", "Alpha") }, out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("Fake name or id cannot be empty.", error);
    }

    [Fact]
    public void ScopedRegistryHit_OutsideCandidates_IsRejected()
    {
        var outside = new Fake("reg_out", "s_out", "Outside");
        var inside = new Fake("reg_in", "s_in", "Inside");
        var registry = new Dictionary<string, Fake> { ["reg_out"] = outside, ["reg_in"] = inside };

        var rejected = Match("reg_out", new[] { inside }, registry, true, out _, out var error);
        var accepted = Match("reg_in", new[] { inside }, registry, true, out var value, out _);

        Assert.False(rejected);
        Assert.Equal("No Fake found with id or name 'reg_out'.", error);
        Assert.True(accepted);
        Assert.Same(inside, value);
    }

    [Fact]
    public void TryResolveGeneric_RejectsObjectOutsideCandidates()
    {
        var outside = new Fake("reg_out", "s_out", "Outside");
        var inside = new Fake("reg_in", "s_in", "Inside");
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(m => m.TryGetObject<Fake>("reg_out", out outside)).Returns(true);
        var resolver = new CommandObjectResolver(objectManager.Object);

        var ok = resolver.TryResolve("reg_out", new[] { inside }, fake => fake.Name, out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("No Fake found with id or name 'reg_out'.", error);
    }

    [Fact]
    public void TryResolveGeneric_ResolvesByNameAndByRegistryId()
    {
        var inside = new Fake("reg_in", "s_in", "Inside");
        var objectManager = new Mock<IObjectManager>();
        string registryId = "reg_in";
        objectManager.Setup(m => m.TryGetId(inside, out registryId)).Returns(true);
        var resolver = new CommandObjectResolver(objectManager.Object);

        Assert.True(resolver.TryResolve("inside", new[] { inside }, fake => fake.Name, out var byName, out _));
        Assert.Same(inside, byName);

        Assert.True(resolver.TryResolve("reg_in", new[] { inside }, fake => "other", out var byId, out _));
        Assert.Same(inside, byId);
    }

    [Fact]
    public void TryResolveGeneric_NullCandidates_Fails()
    {
        var resolver = new CommandObjectResolver(new Mock<IObjectManager>().Object);

        var ok = resolver.TryResolve<Fake>("x", null, fake => fake.Name, out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryResolveGeneric_EmptyQuery_Fails()
    {
        var resolver = new CommandObjectResolver(new Mock<IObjectManager>().Object);

        var ok = resolver.TryResolve(" ", new[] { new Fake("a", "b", "c") }, fake => fake.Name, out _, out var error);

        Assert.False(ok);
        Assert.Equal("Fake name or id cannot be empty.", error);
    }

    [Fact]
    public void TryResolveGeneric_ThrowingNameGetter_SkipsThatCandidate()
    {
        var broken = new Fake("reg_broken", "s_broken", "Broken");
        var target = new Fake("reg_target", "s_target", "Target");
        var resolver = new CommandObjectResolver(new Mock<IObjectManager>().Object);

        var ok = resolver.TryResolve(
            "target",
            new[] { broken, target },
            fake => fake == broken ? throw new NullReferenceException() : fake.Name,
            out var value,
            out _);

        Assert.True(ok);
        Assert.Same(target, value);
    }
}
