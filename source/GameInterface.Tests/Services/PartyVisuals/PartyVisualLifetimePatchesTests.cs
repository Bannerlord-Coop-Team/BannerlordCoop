using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.PartyVisuals.Messages;
using GameInterface.Services.PartyVisuals.Patches;
using SandBox.View.Map.Visuals;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.PartyVisuals;

/// <summary>
/// Tests for the skip-patches (AllowedThread) branch of the party visual lifetime patch. A visual
/// removed by a destroy nested inside another replicated action must still replicate from the
/// server, or clients keep the party's map banner behind as a zombie visual.
/// </summary>
public class PartyVisualLifetimePatchesTests : IDisposable
{
    private readonly List<object> published = new();

    // Subscriptions are weakly referenced by the broker; keep a strong reference for the test's lifetime.
    private readonly Action<MessagePayload<PartyVisualDestroyed>> captureDestroyed;

    private readonly bool wasServer;

    public PartyVisualLifetimePatchesTests()
    {
        wasServer = ModInformation.IsServer;

        captureDestroyed = payload => published.Add(payload.What);

        MessageBroker.Instance.Subscribe(captureDestroyed);
    }

    public void Dispose()
    {
        MessageBroker.Instance.Unsubscribe(captureDestroyed);

        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void OnPartyRemovedPostfix_NestedOnServer_PublishesPartyVisualDestroyed()
    {
        var visual = ObjectHelper.SkipConstructor<MobilePartyVisual>();
        ModInformation.IsServer = true;

        using (new AllowedThread())
        {
            PartyVisualLifetimePatches.OnMobilePartyDestroyedPostfix(ref visual);
        }

        var message = Assert.IsType<PartyVisualDestroyed>(Assert.Single(published));
        Assert.Same(visual, message.MobilePartyVisual);
    }

    [Fact]
    public void OnPartyRemovedPostfix_NestedOnClient_DoesNotPublish()
    {
        var visual = ObjectHelper.SkipConstructor<MobilePartyVisual>();
        ModInformation.IsServer = false;

        using (new AllowedThread())
        {
            PartyVisualLifetimePatches.OnMobilePartyDestroyedPostfix(ref visual);
        }

        Assert.Empty(published);
    }
}
