using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// Covers the prefix on ConversationManager.OpenMapConversation that reroutes a local player's map conversation
/// with another player's party into the synced player-party interaction pipeline instead of letting vanilla
/// open a local, unsynced one.
/// </summary>
[Collection(PlayerPartyInteractionStaticsCollection.Name)]
public class PlayerToPlayerMapConversationRedirectPatchTests : IDisposable
{
    private const string LocalControllerId = "PlayerOne";
    private const string OtherControllerId = "PlayerTwo";

    private static readonly ConditionalWeakTable<object, ControlledObjectInfo> PlayerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;

    private static readonly PropertyInfo PartyBaseMobileParty =
        typeof(PartyBase).GetProperty(nameof(PartyBase.MobileParty))!;

    private static readonly FieldInfo DialogHasState =
        typeof(PlayerPartyInteractionDialogState).GetField("hasState", BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly bool wasServer = ModInformation.IsServer;
    private readonly List<MobileParty> registeredParties = new();

    public PlayerToPlayerMapConversationRedirectPatchTests()
    {
        ModInformation.IsServer = false;
        PlayerPartyInteractionDialogState.Clear();
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        PlayerPartyInteractionDialogState.Clear();
        foreach (var party in registeredParties)
        {
            PlayerObjects.Remove(party);
        }
    }
    
    [Fact]
    public void ShouldRedirect_LocalPlayerToAnotherPlayer_IsTrue()
    {
        var self = LocalPlayerParty();
        var other = OtherPlayerParty();

        Assert.True(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(self, other));
    }

    [Fact]
    public void ShouldRedirect_SelfNull_IsFalse()
    {
        Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(null, OtherPlayerParty()));
    }

    [Fact]
    public void ShouldRedirect_OtherNull_IsFalse()
    {
        Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(LocalPlayerParty(), null));
    }

    [Fact]
    public void ShouldRedirect_TalkingToYourself_IsFalse()
    {
        var self = LocalPlayerParty();

        Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(self, self));
    }

    [Fact]
    public void ShouldRedirect_OtherIsNotAPlayerParty_IsFalse()
    {
        var self = LocalPlayerParty();
        var other = UnregisteredParty();

        Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(self, other));
    }

    [Fact]
    public void ShouldRedirect_SelfIsAnotherPlayersParty_IsFalse()
    {
        // Both sides are player parties but the local instance does not control the "self" side.
        var self = OtherPlayerParty();
        var other = OtherPlayerParty();

        Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(self, other));
    }

    [Fact]
    public void ShouldRedirect_SelfIsNotAPlayerPartyAtAll_IsFalse()
    {
        var self = UnregisteredParty();
        var other = OtherPlayerParty();

        Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(self, other));
    }

    [Fact]
    public void ShouldRedirect_CoopDialogAlreadyActive_IsFalse()
    {
        var self = LocalPlayerParty();
        var other = OtherPlayerParty();

        DialogHasState.SetValue(null, true);
        try
        {
            Assert.False(PlayerToPlayerMapConversationRedirectPatch.ShouldRedirect(self, other));
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear();
        }
    }
    
    [Fact]
    public void Prefix_LocalPlayerToAnotherPlayer_BlocksVanillaAndPublishesRequest()
    {
        var selfParty = Wrap(LocalPlayerParty());
        var otherParty = Wrap(OtherPlayerParty());

        var published = new List<ConversationRequested>();
        Action<MessagePayload<ConversationRequested>> capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            var runOriginal = PlayerToPlayerMapConversationRedirectPatch.Prefix(Conv(selfParty), Conv(otherParty));

            Assert.False(runOriginal);
            var request = Assert.Single(published);
            Assert.Same(otherParty, request.DefenderParty);
            Assert.Same(selfParty, request.AttackerParty);
            Assert.True(request.ArmyTalkEncounter);
            Assert.False(request.ForcePlayerOutFromSettlement);
            Assert.Equal(ConversationRestartSource.PlayerEncounter, request.Source);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Fact]
    public void Prefix_OnServer_RunsOriginalAndPublishesNothing()
    {
        ModInformation.IsServer = true;

        var selfParty = Wrap(LocalPlayerParty());
        var otherParty = Wrap(OtherPlayerParty());

        var published = new List<ConversationRequested>();
        Action<MessagePayload<ConversationRequested>> capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            var runOriginal = PlayerToPlayerMapConversationRedirectPatch.Prefix(Conv(selfParty), Conv(otherParty));

            Assert.True(runOriginal);
            Assert.Empty(published);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Fact]
    public void Prefix_MissingParty_RunsOriginalAndPublishesNothing()
    {
        var otherParty = Wrap(OtherPlayerParty());

        var published = new List<ConversationRequested>();
        Action<MessagePayload<ConversationRequested>> capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            var runOriginal = PlayerToPlayerMapConversationRedirectPatch.Prefix(
                default, Conv(otherParty));

            Assert.True(runOriginal);
            Assert.Empty(published);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Fact]
    public void Prefix_PlayerToNpc_RunsOriginalAndPublishesNothing()
    {
        var selfParty = Wrap(LocalPlayerParty());
        var npcParty = Wrap(UnregisteredParty());

        var published = new List<ConversationRequested>();
        Action<MessagePayload<ConversationRequested>> capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            var runOriginal = PlayerToPlayerMapConversationRedirectPatch.Prefix(Conv(selfParty), Conv(npcParty));

            Assert.True(runOriginal);
            Assert.Empty(published);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Fact]
    public void Prefix_CoopDialogAlreadyActive_RunsOriginalAndPublishesNothing()
    {
        var selfParty = Wrap(LocalPlayerParty());
        var otherParty = Wrap(OtherPlayerParty());

        var published = new List<ConversationRequested>();
        Action<MessagePayload<ConversationRequested>> capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        DialogHasState.SetValue(null, true);
        try
        {
            var runOriginal = PlayerToPlayerMapConversationRedirectPatch.Prefix(Conv(selfParty), Conv(otherParty));

            Assert.True(runOriginal);
            Assert.Empty(published);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
            PlayerPartyInteractionDialogState.Clear();
        }
    }
    
    private MobileParty LocalPlayerParty()
    {
        return RegisterParty(ObjectHelper.SkipConstructor<MobileParty>(), LocalControllerId);
    }

    private MobileParty OtherPlayerParty()
    {
        return RegisterParty(ObjectHelper.SkipConstructor<MobileParty>(), OtherControllerId);
    }

    private static MobileParty UnregisteredParty()
    {
        return ObjectHelper.SkipConstructor<MobileParty>();
    }

    private MobileParty RegisterParty(MobileParty party, string objectControllerId)
    {
        var provider = new ControllerIdProvider();
        provider.SetControllerId(LocalControllerId);
        PlayerObjects.Add(party, new ControlledObjectInfo(objectControllerId, provider));
        registeredParties.Add(party);
        return party;
    }

    private static PartyBase Wrap(MobileParty party)
    {
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        PartyBaseMobileParty.SetValue(partyBase, party);
        return partyBase;
    }

    private static ConversationCharacterData Conv(PartyBase party)
    {
        var data = default(ConversationCharacterData);
        data.Party = party;
        return data;
    }
}
