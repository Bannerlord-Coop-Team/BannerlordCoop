using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Players.Data;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>Verifies how coop.unstuck on the server picks its target from a controller id or hero name.</summary>
public class UnstuckPlayerTargetResolutionTests
{
    private static readonly Player Mira = new Player("76561198000000001", "hero_mira", "party_mira", null, null);
    private static readonly Player Arwa = new Player("76561198000000002", "hero_arwa", "party_arwa", null, null);
    private static readonly Player SecondArwa = new Player("76561198000000003", "hero_arwa_2", "party_arwa_2", null, null);
    private static readonly Player Unnamed = new Player("76561198000000004", "hero_gone", "party_gone", null, null);

    private static readonly Dictionary<Player, string> Names = new Dictionary<Player, string>
    {
        { Mira, "Lady Mira" },
        { Arwa, "Arwa" },
        { SecondArwa, "arwa" },
        { Unnamed, null },
    };

    private static bool Resolve(string query, out Player target, out string error, params Player[] players) =>
        UnstuckCommand.TryResolveTarget(query, players, player => Names[player], out target, out error);

    [Fact]
    public void ControllerId_ResolvesThatPlayer()
    {
        Assert.True(Resolve("76561198000000002", out var target, out _, Mira, Arwa));

        Assert.Same(Arwa, target);
    }

    [Fact]
    public void ControllerId_WinsOverAHeroNamedTheSame()
    {
        var namedLikeAnId = new Player("76561198000000005", "hero_id_name", "party_id_name", null, null);
        var players = new[] { namedLikeAnId, Mira };

        Assert.True(UnstuckCommand.TryResolveTarget(
            "76561198000000001",
            players,
            player => player == namedLikeAnId ? "76561198000000001" : "Lady Mira",
            out var target,
            out _));

        Assert.Same(Mira, target);
    }

    [Fact]
    public void HeroNameWithSpace_MatchesIgnoringCase()
    {
        Assert.True(Resolve("lady MIRA", out var target, out _, Mira, Arwa));

        Assert.Same(Mira, target);
    }

    [Fact]
    public void AmbiguousHeroName_FailsListingOnlyTheMatches()
    {
        Assert.False(Resolve("ARWA", out var target, out var error, Mira, Arwa, SecondArwa));

        Assert.Null(target);
        Assert.Contains("Several players are named 'ARWA'", error);
        Assert.Contains("76561198000000002 (Arwa)", error);
        Assert.Contains("76561198000000003 (arwa)", error);
        Assert.DoesNotContain("76561198000000001", error);
    }

    [Fact]
    public void UnknownName_FailsListingEveryCandidate()
    {
        Assert.False(Resolve("Derthert", out var target, out var error, Mira, Unnamed));

        Assert.Null(target);
        Assert.Contains("No registered player has the controller id or hero name 'Derthert'", error);
        Assert.Contains("76561198000000001 (Lady Mira)", error);
        Assert.Contains("76561198000000004 (<unknown hero>)", error);
        Assert.Contains("coop.debug.players.list", error);
    }

    [Fact]
    public void NoRegisteredPlayers_FailsWithNoCandidates()
    {
        Assert.False(Resolve("Lady Mira", out var target, out var error));

        Assert.Null(target);
        Assert.Contains("Candidates: none.", error);
    }

    [Fact]
    public void PartialName_DoesNotMatch()
    {
        Assert.False(Resolve("Mira", out var target, out _, Mira));

        Assert.Null(target);
    }
}
