using Common.Util;
using GameInterface.Services.Kingdoms;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomRegistryTests
{
    [Fact]
    public void EnsureRuntimeCollections_DoesNotReplaceExistingCollections()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        var activePolicies = new MBList<PolicyObject>();
        var armies = new MBList<Army>();
        var clans = new MBList<Clan>();
        var unresolvedDecisions = new MBList<TaleWorlds.CampaignSystem.Election.KingdomDecision>();
        var factionsAtWarWith = new MBList<IFaction>();
        var alliedKingdoms = new MBList<Kingdom>();
        var fiefs = new MBList<Town>();
        var towns = new MBList<Town>();
        var settlements = new MBList<Settlement>();
        var villages = new MBList<Village>();
        var heroes = new MBList<Hero>();
        var aliveLords = new MBList<Hero>();
        var deadLords = new MBList<Hero>();
        var warParties = new MBList<WarPartyComponent>();

        kingdom._activePolicies = activePolicies;
        kingdom._armies = armies;
        kingdom._clans = clans;
        kingdom._unresolvedDecisions = unresolvedDecisions;
        kingdom._factionsAtWarWith = factionsAtWarWith;
        kingdom._alliedKingdoms = alliedKingdoms;
        kingdom._fiefsCache = fiefs;
        kingdom._townsCache = towns;
        kingdom._settlementsCache = settlements;
        kingdom._villagesCache = villages;
        kingdom._heroesCache = heroes;
        kingdom._aliveLordsCache = aliveLords;
        kingdom._deadLordsCache = deadLords;
        kingdom._warPartyComponentsCache = warParties;

        KingdomRegistry.EnsureRuntimeCollections(kingdom);

        Assert.Same(activePolicies, kingdom._activePolicies);
        Assert.Same(armies, kingdom._armies);
        Assert.Same(clans, kingdom._clans);
        Assert.Same(unresolvedDecisions, kingdom._unresolvedDecisions);
        Assert.Same(factionsAtWarWith, kingdom._factionsAtWarWith);
        Assert.Same(alliedKingdoms, kingdom._alliedKingdoms);
        Assert.Same(fiefs, kingdom._fiefsCache);
        Assert.Same(towns, kingdom._townsCache);
        Assert.Same(settlements, kingdom._settlementsCache);
        Assert.Same(villages, kingdom._villagesCache);
        Assert.Same(heroes, kingdom._heroesCache);
        Assert.Same(aliveLords, kingdom._aliveLordsCache);
        Assert.Same(deadLords, kingdom._deadLordsCache);
        Assert.Same(warParties, kingdom._warPartyComponentsCache);
    }

    [Fact]
    public void EnsureRuntimeCollections_InitializesNativeConstructorCollections()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        KingdomRegistry.EnsureRuntimeCollections(kingdom);

        Assert.NotNull(kingdom._activePolicies);
        Assert.NotNull(kingdom._armies);
        Assert.NotNull(kingdom._clans);
        Assert.NotNull(kingdom._unresolvedDecisions);
        Assert.NotNull(kingdom._factionsAtWarWith);
        Assert.NotNull(kingdom._alliedKingdoms);
        Assert.NotNull(kingdom._fiefsCache);
        Assert.NotNull(kingdom._townsCache);
        Assert.NotNull(kingdom._settlementsCache);
        Assert.NotNull(kingdom._villagesCache);
        Assert.NotNull(kingdom._heroesCache);
        Assert.NotNull(kingdom._aliveLordsCache);
        Assert.NotNull(kingdom._deadLordsCache);
        Assert.NotNull(kingdom._warPartyComponentsCache);
        Assert.NotNull(kingdom.EncyclopediaText);
        Assert.NotNull(kingdom.EncyclopediaTitle);
        Assert.NotNull(kingdom.EncyclopediaRulerTitle);
        Assert.Empty(kingdom.UnresolvedDecisions);
    }
}
