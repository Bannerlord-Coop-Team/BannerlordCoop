using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.Heroes.Handlers;

internal class HeroMeetingHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ISessionHeroMeetingDataInterface sessionHeroMeetingDataInterface;

    private HeroMeetingData heroMeetingData;

    public HeroMeetingHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ISessionHeroMeetingDataInterface sessionHeroMeetingDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.sessionHeroMeetingDataInterface = sessionHeroMeetingDataInterface;

        messageBroker.Subscribe<InitializeClientHeroMeetingData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<PlayerMetHero>(Handle);
        messageBroker.Subscribe<NetworkPlayerMetHero>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientHeroMeetingData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<PlayerMetHero>(Handle);
        messageBroker.Unsubscribe<NetworkPlayerMetHero>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientHeroMeetingData> payload)
    {
        heroMeetingData = payload.What.HeroMeetingData;
    }

    private void Handle(MessagePayload<PlayerHeroChanged> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.NewHero, out var playerHeroId)) return;

        // Replace the host save's global meeting state without announcing new client meetings.
        foreach (var hero in Hero.AllAliveHeroes)
        {
            hero._hasMet = false;
            hero.LastMeetingTimeWithPlayer = default;
        }

        if (heroMeetingData?.PlayerLastMeetingTimes?.TryGetValue(playerHeroId, out var meetingTimes) != true || meetingTimes == null)
            return;

        foreach (var meeting in meetingTimes)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(meeting.Key, out var metHero)) continue;

            metHero._hasMet = true;
            metHero.LastMeetingTimeWithPlayer = new CampaignTime(meeting.Value);
        }
    }

    private void Handle(MessagePayload<PlayerMetHero> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.PlayerHero, out var playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MetHero, out var metHeroId)) return;

        // Keep this join snapshot current if the client changes player character again.
        RecordMeeting(heroMeetingData, playerHeroId, metHeroId, payload.What.LastMeetingTime._numTicks);
        network.SendAll(new NetworkPlayerMetHero(
            playerHeroId,
            metHeroId,
            payload.What.LastMeetingTime._numTicks));
    }

    private void Handle(MessagePayload<NetworkPlayerMetHero> payload)
    {
        if (ModInformation.IsClient) return;

        var meeting = payload.What;
        GameThread.RunSafe(() =>
        {
            if (payload.Who is not NetPeer peer || !playerManager.TryGetPlayer(peer, out var player)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(meeting.MetHeroId, out var metHero)) return;

            var firstMeeting = sessionHeroMeetingDataInterface.RecordMeeting(
                player.HeroId,
                meeting.MetHeroId,
                meeting.LastMeetingTimeTicks);
            if (firstMeeting)
                ApplyFirstMeetingRelation(playerHero, metHero);
        }, context: nameof(Handle));
    }

    // Mirrors v1.5 HeroKnownInformationCampaignBehavior.OnPlayerMetHero, which applies this only for the
    // server's own main hero: a calculating player hero loses relation with a town notable the first time the
    // session records that hero meeting them.
    private static void ApplyFirstMeetingRelation(Hero playerHero, Hero metHero)
    {
        if (!metHero.IsNotable || metHero.CurrentSettlement?.IsTown != true) return;

        var relationChange = (int)TraitEffectHelper.GetTraitEffectBonus(
            playerHero,
            DefaultPersonalityTraitEffects.CalculatingNotableRelationEffect);
        if (relationChange != 0)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerHero, metHero, relationChange, showQuickNotification: false);
    }

    private static void RecordMeeting(
        HeroMeetingData data,
        string playerHeroId,
        string metHeroId,
        long lastMeetingTimeTicks)
    {
        if (data?.PlayerLastMeetingTimes == null) return;

        if (!data.PlayerLastMeetingTimes.TryGetValue(playerHeroId, out var meetingTimes) || meetingTimes == null)
        {
            meetingTimes = new Dictionary<string, long>();
            data.PlayerLastMeetingTimes[playerHeroId] = meetingTimes;
        }

        meetingTimes[metHeroId] = lastMeetingTimeTicks;
    }
}
