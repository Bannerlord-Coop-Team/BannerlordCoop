using Common;
using Common.Util;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// v1.5.6 parley with a party on the map (Ctrl+Click) starts a local conversation with a party the server owns,
/// so clients refuse it until #4601 sends it to the server (#4585). Settlement parley is unchanged.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class ParleyCampaignBehaviorPatchesTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose() => ModInformation.IsServer = wasServer;

    private static PartyBase MobilePartyBase()
    {
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        party.MobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        return party;
    }

    private static PartyBase SettlementPartyBase()
    {
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Settlement = ObjectHelper.SkipConstructor<Settlement>();
        return party;
    }

    [Fact]
    public void Client_PartyParley_IsRefused()
    {
        ModInformation.IsServer = false;

        Assert.False(ParleyCampaignBehaviorPatches.StartParleyPrefix(MobilePartyBase()));
    }

    [Fact]
    public void Client_SettlementParley_RunsVanilla()
    {
        ModInformation.IsServer = false;

        Assert.True(ParleyCampaignBehaviorPatches.StartParleyPrefix(SettlementPartyBase()));
    }

    [Fact]
    public void Server_PartyParley_RunsVanilla()
    {
        ModInformation.IsServer = true;

        Assert.True(ParleyCampaignBehaviorPatches.StartParleyPrefix(MobilePartyBase()));
    }

    [Fact]
    public void Patch_ResolvesItsTarget()
    {
        var harmony = new Harmony(nameof(ParleyCampaignBehaviorPatchesTests));
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(ParleyCampaignBehaviorPatches)).Patch();

            // Harmony returns the replacement method, named after the target with a _PatchN suffix.
            Assert.Contains(patched, method => method.Name.Contains(nameof(ParleyCampaignBehavior.StartParley)));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
