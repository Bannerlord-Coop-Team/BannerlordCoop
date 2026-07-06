using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Prevents tests that mutate global role and player-control state from running concurrently.
/// </summary>
[CollectionDefinition(nameof(MobilePartyAiAuthorityCollection), DisableParallelization = true)]
public class MobilePartyAiAuthorityCollection
{
}

/// <summary>
/// Tests the authority boundary for campaign-map AI decisions.
/// </summary>
[Collection(nameof(MobilePartyAiAuthorityCollection))]
public class MobilePartyAIDisablePatchesTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
    private MobileParty? registeredParty;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;

        if (registeredParty != null)
        {
            playerObjects.Remove(registeredParty);
        }
    }

    [Fact]
    public void IsTickAuthority_ServerPlayerParty_ReturnsFalse()
    {
        var party = RegisterPlayerParty("PlayerOne", "Server");
        ModInformation.IsServer = true;

        bool result = MobilePartyAIDisablePatches.IsTickAuthority(party);

        Assert.False(result);
    }

    [Fact]
    public void IsTickAuthority_ServerAiParty_ReturnsTrue()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = true;

        bool result = MobilePartyAIDisablePatches.IsTickAuthority(party);

        Assert.True(result);
    }

    [Fact]
    public void IsTickAuthority_OwningClientParty_ReturnsTrue()
    {
        var party = RegisterPlayerParty("PlayerOne", "PlayerOne");
        ModInformation.IsServer = false;

        bool result = MobilePartyAIDisablePatches.IsTickAuthority(party);

        Assert.True(result);
    }

    [Fact]
    public void IsTickAuthority_RemoteClientParty_ReturnsFalse()
    {
        var party = RegisterPlayerParty("PlayerOne", "PlayerTwo");
        ModInformation.IsServer = false;

        bool result = MobilePartyAIDisablePatches.IsTickAuthority(party);

        Assert.False(result);
    }

    private MobileParty RegisterPlayerParty(string ownerControllerId, string localControllerId)
    {
        var controllerIdProvider = new ControllerIdProvider();
        controllerIdProvider.SetControllerId(localControllerId);

        registeredParty = ObjectHelper.SkipConstructor<MobileParty>();
        playerObjects.Add(
            registeredParty,
            new ControlledObjectInfo(ownerControllerId, controllerIdProvider));

        return registeredParty;
    }
}
