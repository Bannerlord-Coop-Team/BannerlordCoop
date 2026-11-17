using Autofac;
using Common;
using Common.Util;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests for hiding the server's forced party visibility when a client loads the save the server sent.
/// </summary>
// Swaps Campaign.Current, the active container and the process wide role and debug visibility flags, so it runs alone.
[Collection(nameof(CampaignCurrentCollection))]
public class TransferredPartyVisibilityPatchTests
{
    // The installed patch runs on the real OnGameLoaded event of the current campaign.
    [Fact]
    public void Patch_OnGameLoaded_HidesEveryPartyOnClients()
    {
        var harmony = new Harmony(nameof(Patch_OnGameLoaded_HidesEveryPartyOnClients));
        var builder = new ContainerBuilder();
        builder.RegisterType<PartyVisibilitySweep>().As<IPartyVisibilitySweep>();
        using var container = builder.Build();
        var campaign = CampaignWithVisibleParties(2);
        campaign.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());
        var previousCampaign = Campaign.Current;
        bool wasServer = ModInformation.IsServer;
        bool wasForceAllVisible = DebugPartyVisibility.ForceAllVisible;
        bool hadContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            harmony.CreateClassProcessor(typeof(TransferredPartyVisibilityPatch)).Patch();
            Campaign.Current = campaign;
            ModInformation.IsServer = false;
            DebugPartyVisibility.ForceAllVisible = false;

            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                CampaignEventDispatcher.Instance.OnGameLoaded(null);
            }
        }
        finally
        {
            Campaign.Current = previousCampaign;
            ModInformation.IsServer = wasServer;
            DebugPartyVisibility.ForceAllVisible = wasForceAllVisible;
            if (hadContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
            harmony.UnpatchAll(harmony.Id);
        }

        Assert.Equal(2, campaign.MobileParties.Count);
        Assert.All(campaign.MobileParties, party => Assert.False(party._isVisible));
    }

    private static Campaign CampaignWithVisibleParties(int count)
    {
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.CampaignObjectManager = new CampaignObjectManager();
        for (int i = 0; i < count; i++)
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party._isVisible = true;
            campaign.CampaignObjectManager._mobileParties.Add(party);
        }
        return campaign;
    }
}
