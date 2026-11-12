using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages.LordConversations;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

[Collection(nameof(CampaignCurrentCollection))]
public class LordConversationsCampaignBehaviorPatchesTests
{
    private static Hero OneToOneConversationHero = null!;

    [Fact]
    public void FailsToReleasePrisoner_RemovesHeroFromPendingConversationQueue()
    {
        var previousCampaign = Campaign.Current;
        var harmony = new Harmony($"{nameof(LordConversationsCampaignBehaviorPatchesTests)}.{Guid.NewGuid():N}");
        var conversationHero = ObjectHelper.SkipConstructor<Hero>();
        conversationHero._heroState = Hero.CharacterStates.Prisoner;
        var conversationCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        conversationCharacter.HeroObject = conversationHero;

        var otherHero = ObjectHelper.SkipConstructor<Hero>();
        var otherCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        otherCharacter.HeroObject = otherHero;

        var pendingHeroes = new List<TroopRosterElement>
        {
            new TroopRosterElement(conversationCharacter),
            new TroopRosterElement(otherCharacter),
            new TroopRosterElement(conversationCharacter),
        };

        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._capturedAlreadyPrisonerHeroes = pendingHeroes;

        var mainParty = ObjectHelper.SkipConstructor<PartyBase>();
        var mainMobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        mainMobileParty.Party = mainParty;

        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.MainParty = mainMobileParty;
        campaign.PlayerEncounter = encounter;

        var published = new List<TakeLordPrisoner>();
        Action<MessagePayload<TakeLordPrisoner>> capture = payload => published.Add(payload.What);

        try
        {
            OneToOneConversationHero = conversationHero;
            Campaign.Current = campaign;
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero)),
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(LordConversationsCampaignBehaviorPatchesTests),
                    nameof(GetOneToOneConversationHeroPrefix))));
            MessageBroker.Instance.Subscribe(capture);

            bool runOriginal = LordConversationsCampaignBehaviorPatches
                .ConversationPlayerFailsToReleasePrisonerOnConsequencePrefix();

            Assert.False(runOriginal);
            TakeLordPrisoner request = Assert.Single(published);
            Assert.Same(mainParty, request.MainParty);
            Assert.Same(conversationHero, request.ConversationHero);
            Assert.Single(pendingHeroes);
            Assert.Same(otherCharacter, pendingHeroes[0].Character);
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
            harmony.UnpatchAll(harmony.Id);
            Campaign.Current = previousCampaign;
            OneToOneConversationHero = null!;
        }
    }

    private static bool GetOneToOneConversationHeroPrefix(ref Hero __result)
    {
        __result = OneToOneConversationHero;
        return false;
    }
}
