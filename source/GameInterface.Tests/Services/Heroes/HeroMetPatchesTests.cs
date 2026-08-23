using Common;
using Common.Messaging;
using Common.Util;
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
    public void SetHasMetPostfix_OnClient_PublishesPlayerMeeting()
    {
        var wasServer = ModInformation.IsServer;
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var metHero = ObjectHelper.SkipConstructor<Hero>();
        PlayerMetHero publishedMeeting = null;

        void Handle(MessagePayload<PlayerMetHero> payload) => publishedMeeting = payload.What;

        ModInformation.IsServer = false;
        MessageBroker.Instance.Subscribe<PlayerMetHero>(Handle);
        try
        {
            HeroMetPatches.SetHasMetPostfix(metHero, playerHero);

            Assert.Same(playerHero, publishedMeeting.PlayerHero);
            Assert.Same(metHero, publishedMeeting.MetHero);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe<PlayerMetHero>(Handle);
            ModInformation.IsServer = wasServer;
        }
    }
}