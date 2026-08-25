using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

/// <summary>
/// Tests that meeting a hero on a client sends the meeting event used for persistence.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class HeroMetPatchesTests
{
    [Fact]
    public void SetHasMetPostfix_WithCapturedPlayerHero_PublishesPlayerMeeting()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var metHero = ObjectHelper.SkipConstructor<Hero>();
        PlayerMetHero? publishedMeeting = null;
        var publishCount = 0;

        void Handle(MessagePayload<PlayerMetHero> payload)
        {
            publishCount++;
            publishedMeeting = payload.What;
        }

        MessageBroker.Instance.Subscribe<PlayerMetHero>(Handle);
        try
        {
            HeroMetPatches.SetHasMetPostfix(metHero, playerHero);

            Assert.Equal(1, publishCount);
            Assert.NotNull(publishedMeeting);
            Assert.Same(playerHero, publishedMeeting.PlayerHero);
            Assert.Same(metHero, publishedMeeting.MetHero);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe<PlayerMetHero>(Handle);
        }
    }

    [Fact]
    public void SetHasMetPrefix_OnServer_DoesNotCapturePlayerHero()
    {
        var wasServer = ModInformation.IsServer;

        ModInformation.IsServer = true;
        try
        {
            HeroMetPatches.SetHasMetPrefix(out var state);

            Assert.Null(state);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void SetHasMetPrefix_OnAllowedThread_DoesNotCapturePlayerHero()
    {
        var wasServer = ModInformation.IsServer;

        ModInformation.IsServer = false;
        try
        {
            using (new AllowedThread())
            {
                HeroMetPatches.SetHasMetPrefix(out var state);

                Assert.Null(state);
            }
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void SetHasMetPrefix_WhenOriginalsAllowedOnAllThreads_DoesNotCapturePlayerHero()
    {
        var wasServer = ModInformation.IsServer;

        ModInformation.IsServer = false;
        try
        {
            using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
            {
                HeroMetPatches.SetHasMetPrefix(out var state);

                Assert.Null(state);
            }
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
