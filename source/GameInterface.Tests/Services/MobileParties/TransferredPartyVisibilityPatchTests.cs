using Common;
using Common.Util;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests for hiding the server's forced party visibility when a client loads the save the server sent.
/// </summary>
// Swaps Campaign.Current and the process wide role and debug visibility flags, so it runs alone.
[Collection(nameof(CampaignCurrentCollection))]
public class TransferredPartyVisibilityPatchTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly bool wasForceAllVisible = DebugPartyVisibility.ForceAllVisible;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        DebugPartyVisibility.ForceAllVisible = wasForceAllVisible;
    }

    // Release clients start every party hidden; the server and debug builds keep them all visible.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ClearServerVisibility_HidesEveryPartyOnlyOnReleaseClients(bool isServer, bool forceAllVisible)
    {
        var campaign = CampaignWithVisibleParties(3);
        ModInformation.IsServer = isServer;
        DebugPartyVisibility.ForceAllVisible = forceAllVisible;

        TransferredPartyVisibilityPatch.ClearServerVisibility(campaign);

        bool stayVisible = isServer || forceAllVisible;
        Assert.Equal(3, campaign.MobileParties.Count);
        Assert.All(campaign.MobileParties, party => Assert.Equal(stayVisible, party._isVisible));
    }

    // A late initialize load re-reads each party's saved fields in InitializeObjects (MobileParty.OnLoad), so the
    // clear only holds after it. CampaignEventDispatcher.OnGameLoaded runs after InitializeObjects.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearServerVisibility_HoldsOnlyAfterLateInitialize(bool clearAfterInitialize)
    {
        var saved = ObjectHelper.SkipConstructor<MobileParty>();
        saved.Party = ObjectHelper.SkipConstructor<PartyBase>();
        saved._isVisible = true;
        var driver = new InMemDriver();
        var saveResult = SaveManager.Save(saved, new MetaData { ["ApplicationVersion"] = "v1.4.8.0" }, "party", driver);
        Assert.True(saveResult.Successful);
        var loadResult = SaveManager.Load("party", driver, loadAsLateInitialize: true);
        var party = Assert.IsType<MobileParty>(loadResult.Root);
        Assert.True(party._isVisible);
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.CampaignObjectManager = new CampaignObjectManager();
        campaign.CampaignObjectManager._mobileParties.Add(party);
        ModInformation.IsServer = false;
        DebugPartyVisibility.ForceAllVisible = false;

        if (!clearAfterInitialize) TransferredPartyVisibilityPatch.ClearServerVisibility(campaign);
        loadResult.InitializeObjects();
        if (clearAfterInitialize) TransferredPartyVisibilityPatch.ClearServerVisibility(campaign);

        Assert.Equal(!clearAfterInitialize, party._isVisible);
    }

    // The installed patch runs on the real OnGameLoaded event of the current campaign.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Patch_OnGameLoaded_HidesPartiesOnlyOnClients(bool isServer)
    {
        var harmony = new Harmony(nameof(Patch_OnGameLoaded_HidesPartiesOnlyOnClients));
        var previousCampaign = Campaign.Current;
        var campaign = CampaignWithVisibleParties(2);
        campaign.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());
        try
        {
            harmony.CreateClassProcessor(typeof(TransferredPartyVisibilityPatch)).Patch();
            Campaign.Current = campaign;
            ModInformation.IsServer = isServer;
            DebugPartyVisibility.ForceAllVisible = false;

            CampaignEventDispatcher.Instance.OnGameLoaded(null);
        }
        finally
        {
            Campaign.Current = previousCampaign;
            harmony.UnpatchAll(harmony.Id);
        }

        Assert.All(campaign.MobileParties, party => Assert.Equal(isServer, party._isVisible));
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
