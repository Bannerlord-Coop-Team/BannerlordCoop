using Common.Util;
using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace E2E.Tests.Services.Settlements;

/// <summary>
/// A town of its own clan that goes to war with a party's clan, with vanilla's diplomatic settlement check registered.
/// </summary>
internal static class HostileTownSetup
{
    /// <summary>
    /// Puts the parties in a new town on every instance. Only <paramref name="atWarClanId"/> gets a faction id, the one the war targets.
    /// </summary>
    internal static (string SettlementId, string TownClanId) CreateTown(E2ETestEnvironment environment, string atWarClanId, params string[] partyIds)
    {
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var townClanId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                var town = instance.GetRegisteredObject<Town>(townId);
                var townClan = instance.GetRegisteredObject<Clan>(townClanId);
                // Stance links are keyed by faction id, which the harness leaves unset
                townClan.Id = new MBGUID(4857);
                instance.GetRegisteredObject<Clan>(atWarClanId).Id = new MBGUID(4858);
                settlement.SetSettlementComponent(town);
                town._ownerClan = townClan;
                townClan._settlementsCache.Add(settlement);
                foreach (var partyId in partyIds)
                    instance.GetRegisteredObject<MobileParty>(partyId).CurrentSettlement = settlement;

                var diplomaticHandler = new PartyDiplomaticHandlerCampaignBehavior();
                diplomaticHandler.RegisterEvents();
                // Only the server's parking path finds the behavior through GetCampaignBehavior, which needs the manager
                if (instance == environment.Server)
                    Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { diplomaticHandler }));
            });
        }
        return (settlementId, townClanId);
    }

    internal static void DeclareWar(E2ETestEnvironment environment, string townClanId, string atWarClanId)
    {
        environment.Server.Call(() => DeclareWarAction.ApplyInternal(
            environment.Server.GetRegisteredObject<Clan>(townClanId),
            environment.Server.GetRegisteredObject<Clan>(atWarClanId),
            DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision));
        Flush(environment);
    }

    internal static void AssertInTown(E2ETestEnvironment environment, string partyId, string settlementId, bool inTown)
    {
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            instance.Call(() => Assert.Same(
                inTown ? instance.GetRegisteredObject<Settlement>(settlementId) : null,
                instance.GetRegisteredObject<MobileParty>(partyId).CurrentSettlement));
        }
    }

    // Two passes, so a reply the server sends from a game-thread action reaches the clients too
    internal static void Flush(E2ETestEnvironment environment)
    {
        for (int i = 0; i < 2; i++)
        {
            environment.Server.PumpGameThread();
            environment.FlushCoalescer();
            foreach (var client in environment.Clients) client.PumpGameThread();
        }
    }
}
