using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests that <see cref="BattleAgentAuthorityRegistry"/> forwards to the attached battle and answers null otherwise.</summary>
public class BattleAgentAuthorityRegistryTests
{
    // With no battle attached every answer is null, so callers fall back to their non-battle checks.
    [Fact]
    public void Answers_NullBeforeAnyBattleAttaches()
    {
        var registry = new BattleAgentAuthorityRegistry();

        Assert.Null(registry.IsMountRemotelyOwned(null));
        Assert.Null(registry.IsHeroAgentLocallyOwned(null));
    }

    // An attached battle answers both questions.
    [Fact]
    public void Answers_ForwardToAttachedBattle()
    {
        var registry = new BattleAgentAuthorityRegistry();

        registry.Attach(new FixedAuthority(mountRemotelyOwned: true, heroAgentLocallyOwned: false));

        Assert.True(registry.IsMountRemotelyOwned(null));
        Assert.False(registry.IsHeroAgentLocallyOwned(null));
    }

    // Detaching the attached battle clears its answers.
    [Fact]
    public void Detach_SameBattle_ClearsAnswers()
    {
        var registry = new BattleAgentAuthorityRegistry();
        var battle = new FixedAuthority(mountRemotelyOwned: true, heroAgentLocallyOwned: true);

        registry.Attach(battle);
        registry.Detach(battle);

        Assert.Null(registry.IsMountRemotelyOwned(null));
        Assert.Null(registry.IsHeroAgentLocallyOwned(null));
    }

    // A stale battle disposed after a newer one attached must not clear the newer one's answers.
    [Fact]
    public void Detach_StaleBattle_KeepsNewerAnswers()
    {
        var registry = new BattleAgentAuthorityRegistry();
        var stale = new FixedAuthority(mountRemotelyOwned: true, heroAgentLocallyOwned: true);
        var newer = new FixedAuthority(mountRemotelyOwned: false, heroAgentLocallyOwned: false);

        registry.Attach(stale);
        registry.Attach(newer);
        registry.Detach(stale);

        Assert.False(registry.IsMountRemotelyOwned(null));
        Assert.False(registry.IsHeroAgentLocallyOwned(null));
    }

    /// <summary>Battle stand-in that returns the same ownership answers for any agent or hero.</summary>
    private sealed class FixedAuthority : IBattleAgentAuthority
    {
        private readonly bool? mountRemotelyOwned;
        private readonly bool? heroAgentLocallyOwned;

        // Stores the answers to return.
        public FixedAuthority(bool? mountRemotelyOwned, bool? heroAgentLocallyOwned)
        {
            this.mountRemotelyOwned = mountRemotelyOwned;
            this.heroAgentLocallyOwned = heroAgentLocallyOwned;
        }

        // Returns the fixed mount answer.
        public bool? IsMountRemotelyOwned(Agent mount) => mountRemotelyOwned;

        // Returns the fixed hero answer.
        public bool? IsHeroAgentLocallyOwned(Hero hero) => heroAgentLocallyOwned;
    }
}
