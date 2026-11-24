using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Equipments.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments.Handlers;

/// <summary>
/// Applies a client's main hero horse loss on the server, so the slot and roster writes replicate.
/// </summary>
internal class MainHeroMountHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public MainHeroMountHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<MainHeroMountKilled>(Handle_MainHeroMountKilled);
        messageBroker.Subscribe<NetworkMainHeroMountKilled>(Handle_NetworkMainHeroMountKilled);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MainHeroMountKilled>(Handle_MainHeroMountKilled);
        messageBroker.Unsubscribe<NetworkMainHeroMountKilled>(Handle_NetworkMainHeroMountKilled);
    }

    private void Handle_MainHeroMountKilled(MessagePayload<MainHeroMountKilled> payload)
    {
        var data = payload.What;
        if (!objectManager.TryGetIdWithLogging(data.Hero, out var heroId)) return;

        network.SendAll(new NetworkMainHeroMountKilled(heroId, data.UsesCivilianEquipment, data.Loss));
    }

    private void Handle_NetworkMainHeroMountKilled(MessagePayload<NetworkMainHeroMountKilled> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(payload.Who, data.HeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;

            Equipment equipment = data.UsesCivilianEquipment ? hero.CivilianEquipment : hero.BattleEquipment;

            switch (data.Loss)
            {
                case MountLoss.Lost:
                    EquipmentElement harness = equipment[EquipmentIndex.HorseHarness];
                    equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
                    equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

                    // Vanilla keeps the harness, adding it to the rider's party roster
                    if (!harness.IsInvalid() && !harness.IsEmpty)
                    {
                        hero.PartyBelongedTo?.ItemRoster.AddToCounts(harness, 1);
                    }
                    break;
                case MountLoss.Lamed:
                    if (!objectManager.TryGetObjectWithLogging<ItemModifier>(MainHeroMountKilled.LameHorseModifierId, out var lameModifier)) return;
                    equipment[EquipmentIndex.Horse] = new EquipmentElement(equipment[EquipmentIndex.Horse].Item, lameModifier);
                    break;
            }
        }, context: nameof(NetworkMainHeroMountKilled));
    }
}
