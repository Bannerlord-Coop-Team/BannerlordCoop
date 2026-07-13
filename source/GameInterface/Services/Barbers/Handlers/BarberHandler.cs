using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Barbers.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Barbers.Handlers;

internal class BarberHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BarberHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public BarberHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<BarberChargesPlayer>(Handle_BarberChargesPlayer);
        messageBroker.Subscribe<NetworkBarberChargesPlayer>(Handle_NetworkBarberChargesPlayer);
        messageBroker.Subscribe<SaveCurrentCharacter>(Handle_SaveCurrentCharacter);
        messageBroker.Subscribe<NetworkSaveCurrentCharacter>(Handle_NetworkSaveCurrentCharacter);

        messageBroker.Subscribe<NetworkRefreshCharacter>(Handle_NetworkRefreshCharacter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BarberChargesPlayer>(Handle_BarberChargesPlayer);
        messageBroker.Unsubscribe<NetworkBarberChargesPlayer>(Handle_NetworkBarberChargesPlayer);
        messageBroker.Unsubscribe<SaveCurrentCharacter>(Handle_SaveCurrentCharacter);
        messageBroker.Unsubscribe<NetworkSaveCurrentCharacter>(Handle_NetworkSaveCurrentCharacter);

        messageBroker.Unsubscribe<NetworkRefreshCharacter>(Handle_NetworkRefreshCharacter);
    }

    private void Handle_BarberChargesPlayer(MessagePayload<BarberChargesPlayer> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

            network.SendAll(new NetworkBarberChargesPlayer(mainHeroId));
        });
    }

    private void Handle_NetworkBarberChargesPlayer(MessagePayload<NetworkBarberChargesPlayer> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, BarberCampaignBehavior.BarberCost, false);
        });
    }

    private void Handle_SaveCurrentCharacter(MessagePayload<SaveCurrentCharacter> obj)
    {
        GameThread.RunSafe(() =>
        {
            // During character creation the character will not resolve. Avoid logging a failed lookup
            if (!objectManager.TryGetId(obj.What.CharacterToChange, out var characterToChangeId)) return;

            var message = new NetworkSaveCurrentCharacter(characterToChangeId, obj.What.CurrentBodyProperties, obj.What.Race, obj.What.IsFemale);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkSaveCurrentCharacter(MessagePayload<NetworkSaveCurrentCharacter> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(obj.What.CharacterToChangeId, out var characterToChange)) return;

            characterToChange.HeroObject.StaticBodyProperties = obj.What.CurrentBodyProperties.StaticProperties;
            characterToChange.HeroObject.Build = obj.What.CurrentBodyProperties.DynamicProperties.Build;
            characterToChange.HeroObject.Weight = obj.What.CurrentBodyProperties.DynamicProperties.Weight;

            characterToChange.UpdatePlayerCharacterBodyProperties(obj.What.CurrentBodyProperties, obj.What.Race, obj.What.IsFemale);

            UpdateVisuals(characterToChange);
            network.SendAll(new NetworkRefreshCharacter(obj.What.CharacterToChangeId));
        });
    }

    private void Handle_NetworkRefreshCharacter(MessagePayload<NetworkRefreshCharacter> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(obj.What.UpdatedCharacterId, out var character)) return;

            UpdateVisuals(character);
        });
    }

    private void UpdateVisuals(CharacterObject updatedCharacter)
    {
        if (updatedCharacter?.HeroObject?.PartyBelongedTo != null)
        {
            updatedCharacter.HeroObject.PartyBelongedTo.Party.SetVisualAsDirty();
        }

        // Return if not in a mission, nothing to update
        if (Mission.Current == null) return;

        foreach (Agent agent in Mission.Current.Agents)
        {
            CharacterObject currentCharacter = (CharacterObject)agent.Character;
            if (currentCharacter == null) continue;

            if (currentCharacter.IsHero && currentCharacter == updatedCharacter)
            {
                agent.UpdateBodyProperties(updatedCharacter.HeroObject.BodyProperties);
                agent.UpdateSpawnEquipmentAndRefreshVisuals(Mission.Current.DoesMissionRequireCivilianEquipment ? updatedCharacter.FirstCivilianEquipment : updatedCharacter.FirstBattleEquipment);
            }
        }

    }
}
