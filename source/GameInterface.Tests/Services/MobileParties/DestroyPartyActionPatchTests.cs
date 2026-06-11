using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests for the skip-patches (AllowedThread) branch of the party destruction patches. A destroy
/// that runs as a vanilla side effect nested inside another replicated action (e.g. a settlement
/// ownership change culling its patrol) must still replicate from the server, while the client
/// (applying a received destroy under AllowedThread) must stay silent. Replication is also gated
/// on the party being alive, so vanilla re-running an already-applied destruction does not
/// publish a second time.
/// </summary>
public class DestroyPartyActionPatchTests : IDisposable
{
    private readonly List<object> published = new();

    // Subscriptions are weakly referenced by the broker; keep strong references for the test's lifetime.
    private readonly Action<MessagePayload<DestroyPartyApplied>> captureDestroyed;
    private readonly Action<MessagePayload<PartyDisbanded>> captureDisbanded;
    private readonly Action<MessagePayload<InstanceDestroyed<MobilePartyAi>>> captureAiDestroyed;

    private readonly bool wasServer;

    public DestroyPartyActionPatchTests()
    {
        wasServer = ModInformation.IsServer;

        captureDestroyed = payload => published.Add(payload.What);
        captureDisbanded = payload => published.Add(payload.What);
        captureAiDestroyed = payload => published.Add(payload.What);

        MessageBroker.Instance.Subscribe(captureDestroyed);
        MessageBroker.Instance.Subscribe(captureDisbanded);
        MessageBroker.Instance.Subscribe(captureAiDestroyed);
    }

    public void Dispose()
    {
        MessageBroker.Instance.Unsubscribe(captureDestroyed);
        MessageBroker.Instance.Unsubscribe(captureDisbanded);
        MessageBroker.Instance.Unsubscribe(captureAiDestroyed);

        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void PrefixApply_NestedOnServer_PublishesDestroyPartyApplied()
    {
        var party = CreateActiveParty();
        ModInformation.IsServer = true;

        bool runOriginal;
        using (new AllowedThread())
        {
            runOriginal = DestroyPartyActionPatch.PrefixApply(null, party);
        }

        Assert.True(runOriginal);
        var message = Assert.IsType<DestroyPartyApplied>(Assert.Single(published));
        Assert.Null(message.VictoriousPartyBase);
        Assert.Same(party, message.DefeatedParty);
    }

    [Fact]
    public void PrefixApply_NestedOnServer_InactiveParty_DoesNotPublish()
    {
        // SkipConstructor leaves IsActive false: an inactive party is a destruction the
        // replication layer already applied, so it must not replicate again.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = true;

        bool runOriginal;
        using (new AllowedThread())
        {
            runOriginal = DestroyPartyActionPatch.PrefixApply(null, party);
        }

        Assert.True(runOriginal);
        Assert.Empty(published);
    }

    [Fact]
    public void PrefixApply_NestedOnServer_PlayerParty_BlocksWithoutPublish()
    {
        var party = CreateActiveParty();
        // The player-party guard logs the party name; give the uninitialized party a PartyBase
        // so the Name getter does not dereference null.
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party))
            .SetValue(party, ObjectHelper.SkipConstructor<PartyBase>());
        ModInformation.IsServer = true;

        var playerObjects = (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
        playerObjects.Add(party, new ControlledObjectInfo("TestPlayer", null!));

        try
        {
            bool runOriginal;
            using (new AllowedThread())
            {
                runOriginal = DestroyPartyActionPatch.PrefixApply(null, party);
            }

            Assert.False(runOriginal);
            Assert.Empty(published);
        }
        finally
        {
            playerObjects.Remove(party);
        }
    }

    [Fact]
    public void PrefixApply_NestedOnClient_DoesNotPublish()
    {
        var party = CreateActiveParty();
        ModInformation.IsServer = false;

        bool runOriginal;
        using (new AllowedThread())
        {
            runOriginal = DestroyPartyActionPatch.PrefixApply(null, party);
        }

        Assert.True(runOriginal);
        Assert.Empty(published);
    }

    [Fact]
    public void PrefixApplyForDisbanding_NestedOnServer_PublishesPartyDisbanded()
    {
        var party = CreateActiveParty();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        ModInformation.IsServer = true;

        using (new AllowedThread())
        {
            DestroyPartyActionPatch.PrefixApplyForDisbanding(party, settlement);
        }

        var message = Assert.IsType<PartyDisbanded>(Assert.Single(published));
        Assert.Same(party, message.DisbandedParty);
        Assert.Same(settlement, message.RelatedSettlement);
    }

    [Fact]
    public void PrefixApplyForDisbanding_NestedOnServer_InactiveParty_DoesNotPublish()
    {
        // Vanilla re-runs the disband after the replication layer already applied it (the party
        // is inactive by then); the second pass must not publish again.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        ModInformation.IsServer = true;

        using (new AllowedThread())
        {
            DestroyPartyActionPatch.PrefixApplyForDisbanding(party, settlement);
        }

        Assert.Empty(published);
    }

    [Fact]
    public void PrefixApplyForDisbanding_NestedOnClient_DoesNotPublish()
    {
        var party = CreateActiveParty();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        ModInformation.IsServer = false;

        using (new AllowedThread())
        {
            DestroyPartyActionPatch.PrefixApplyForDisbanding(party, settlement);
        }

        Assert.Empty(published);
    }

    [Fact]
    public void RemovePartyPostfix_NestedOnServer_PublishesAiInstanceDestroyed()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var ai = ObjectHelper.SkipConstructor<MobilePartyAi>();
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)).SetValue(party, ai);
        ModInformation.IsServer = true;

        using (new AllowedThread())
        {
            MobilePartyAiLifetimePatches.RemoveParty_Postfix(ref party, __state: true);
        }

        var message = Assert.IsType<InstanceDestroyed<MobilePartyAi>>(Assert.Single(published));
        Assert.Same(ai, message.Instance);
    }

    [Fact]
    public void RemovePartyPostfix_NestedOnServer_WasInactive_DoesNotPublish()
    {
        // __state captures liveness before RemoveParty ran; a removal of an already-inactive
        // party is a re-application and must not replicate the Ai destruction again.
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = true;

        using (new AllowedThread())
        {
            MobilePartyAiLifetimePatches.RemoveParty_Postfix(ref party, __state: false);
        }

        Assert.Empty(published);
    }

    [Fact]
    public void RemovePartyPostfix_NestedOnClient_DoesNotPublish()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        ModInformation.IsServer = false;

        using (new AllowedThread())
        {
            MobilePartyAiLifetimePatches.RemoveParty_Postfix(ref party, __state: true);
        }

        Assert.Empty(published);
    }

    private static MobileParty CreateActiveParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        // Replication is gated on liveness; SkipConstructor leaves IsActive false.
        party.IsActive = true;
        return party;
    }
}
