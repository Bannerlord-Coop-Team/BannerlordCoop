using Common.Logging;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;

internal interface IBountyHuntersQuestContext
{
    bool TryOpen(Hero giver, out IDisposable scope);
}

internal sealed class BountyHuntersQuestContext : IBountyHuntersQuestContext
{
    private static readonly ILogger Logger = LogManager.GetLogger<BountyHuntersQuestContext>();
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public BountyHuntersQuestContext(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects)
    {
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
    }

    public bool TryOpen(Hero giver, out IDisposable scope)
    {
        scope = null;
        if (!ownership.TryGetOwnerControllerId(giver, out var controller) || !players.TryGetPlayer(controller, out var player))
        {
            Logger.Error("Cannot run bounty hunters quest for {Giver}, its player is not registered", giver?.StringId);
            return false;
        }
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        scope = new BountyHuntersOwnerScope(hero, party, giver.Issue?.IsSolvingWithAlternative == true);
        return true;
    }
}

internal sealed class BountyHuntersOwnerScope : IDisposable
{
    [ThreadStatic]
    private static Hero currentOwner;

    internal static Hero CurrentOwner => currentOwner;
    private readonly Hero previousOwner;
    private readonly MainHeroSubstitutionScope heroScope;
    private readonly IssueFinalizeAuthorityGuard finalizeScope;
    private readonly AlternativeSolutionCompletionAuthorityGuard alternativeScope;

    internal BountyHuntersOwnerScope(Hero hero, MobileParty party, bool alternative)
    {
        previousOwner = currentOwner;
        heroScope = new MainHeroSubstitutionScope(hero, party);
        finalizeScope = new IssueFinalizeAuthorityGuard();
        if (alternative) alternativeScope = new AlternativeSolutionCompletionAuthorityGuard();
        currentOwner = hero;
    }

    public void Dispose()
    {
        currentOwner = previousOwner;
        alternativeScope?.Dispose();
        finalizeScope.Dispose();
        heroScope.Dispose();
    }
}
