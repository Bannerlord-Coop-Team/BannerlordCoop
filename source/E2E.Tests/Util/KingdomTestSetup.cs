using Common.Util;
using E2E.Tests.Environment.Instance;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Util;

/// <summary>
/// Stages harness-created kingdoms: fills the lists a constructor-built <see cref="Kingdom"/> leaves null, lists it
/// with the campaign, and puts clans in it, so vanilla kingdom code can run against it.
/// </summary>
internal static class KingdomTestSetup
{
    public static void EnsureKingdomRegistered(IEnumerable<EnvironmentInstance> instances, string kingdomId)
    {
        foreach (var instance in instances)
        {
            EnsureKingdomRegistered(instance, kingdomId);
        }
    }

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

    /// <summary>Puts the clan in the kingdom's membership directly, on the calling instance; wrap it in an AllowedThread.</summary>
    public static void PlaceClanInKingdom(Clan clan, Kingdom kingdom)
    {
        clan._kingdom = kingdom;
        kingdom._rulingClan ??= clan;
        kingdom._clans ??= new MBList<Clan>();
        if (!kingdom._clans.Contains(clan))
        {
            kingdom._clans.Add(clan);
        }
    }
}
