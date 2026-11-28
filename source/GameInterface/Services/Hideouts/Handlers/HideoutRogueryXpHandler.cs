using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Hideouts.Handlers;

/// <summary>Gives the roguery xp a hideout mission earns to every player hero on the raiding side, on the server.</summary>
internal class HideoutRogueryXpHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public HideoutRogueryXpHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<HideoutRogueryXpEarned>(Handle_HideoutRogueryXpEarned);
        messageBroker.Subscribe<NetworkHideoutRogueryXpEarned>(Handle_NetworkHideoutRogueryXpEarned);
        messageBroker.Subscribe<NetworkHideoutGhostBonusGained>(Handle_NetworkHideoutGhostBonusGained);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HideoutRogueryXpEarned>(Handle_HideoutRogueryXpEarned);
        messageBroker.Unsubscribe<NetworkHideoutRogueryXpEarned>(Handle_NetworkHideoutRogueryXpEarned);
        messageBroker.Unsubscribe<NetworkHideoutGhostBonusGained>(Handle_NetworkHideoutGhostBonusGained);
    }

    private void Handle_HideoutRogueryXpEarned(MessagePayload<HideoutRogueryXpEarned> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId)) return;

        network.SendAll(new NetworkHideoutRogueryXpEarned(mapEventId, payload.What.Reward));
    }

    private void Handle_NetworkHideoutRogueryXpEarned(MessagePayload<NetworkHideoutRogueryXpEarned> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;

            var model = Campaign.Current.Models.HideoutModel;
            foreach (var (player, hero, _) in playerManager.GetPlayersOnSide(mapEvent.AttackerSide))
            {
                // Vanilla DefaultSkillLevelingManager, keyed off each raiding player's hero instead of Hero.MainHero.
                float xp = data.Reward == HideoutRogueryXpReward.ClearedAsGhost
                    ? model.GetRogueryXpGainAsGhost()
                    : model.GetRogueryXpGainOnHideoutMissionEnd(data.Reward == HideoutRogueryXpReward.MissionSucceeded);
                float before = hero.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
                hero.AddSkillXp(DefaultSkills.Roguery, xp);
                float gained = hero.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery) - before;

                if (data.Reward == HideoutRogueryXpReward.ClearedAsGhost && playerManager.TryGetPeer(player.ControllerId, out var peer))
                    network.Send(peer, new NetworkHideoutGhostBonusGained(gained, xp));
            }
        });
    }

    private void Handle_NetworkHideoutGhostBonusGained(MessagePayload<NetworkHideoutGhostBonusGained> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            // Vanilla OnHideoutClearedAsGhost's message.
            var message = new TextObject("{=Obuhsttm}Ghost bonus: {XP} Roguery exp! (Base: {BASE_XP})");
            message.SetTextVariable("BASE_XP", MathF.Floor(data.BaseXp));
            message.SetTextVariable("XP", MathF.Floor(data.Xp));
            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), new Color(0f, 0f, 1f)));
        });
    }
}
