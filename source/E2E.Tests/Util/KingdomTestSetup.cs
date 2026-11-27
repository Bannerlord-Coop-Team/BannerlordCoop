using Common.Util;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Util;

/// <summary>
/// Fills the lists a harness-created <see cref="Kingdom"/> leaves null and lists it with the campaign, so vanilla
/// kingdom code can run against it.
/// </summary>
internal static class KingdomTestSetup
{
    public static void EnsureKingdomRegistered(EnvironmentInstance instance, string kingdomId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                kingdom._activePolicies ??= new MBList<PolicyObject>();
                kingdom._armies ??= new MBList<Army>();
                kingdom._clans ??= new MBList<Clan>();
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._factionsAtWarWith ??= new MBList<IFaction>();
                kingdom._alliedKingdoms ??= new MBList<Kingdom>();

                if (!Campaign.Current.CampaignObjectManager.Kingdoms.Contains(kingdom))
                {
                    Campaign.Current.CampaignObjectManager.AddKingdom(kingdom);
                }
            }
        });
    }
}
