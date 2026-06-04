using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Handlers;

internal class VolunteerTypesHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<VolunteerTypesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public VolunteerTypesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<VolunteerRemoved>(Handle_VolunteerRemoved);
        messageBroker.Subscribe<RemoveVolunteer>(Handle_RemoveVolunteer);
        messageBroker.Subscribe<VolunteersUpdated>(Handle_VolunteersUpdated);
        messageBroker.Subscribe<UpdateVolunteers>(Handle_UpdateVolunteers);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VolunteerRemoved>(Handle_VolunteerRemoved);
        messageBroker.Unsubscribe<RemoveVolunteer>(Handle_RemoveVolunteer);
        messageBroker.Unsubscribe<VolunteersUpdated>(Handle_VolunteersUpdated);
        messageBroker.Unsubscribe<UpdateVolunteers>(Handle_UpdateVolunteers);
    }

    private void Handle_VolunteerRemoved(MessagePayload<VolunteerRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Individual, out var individualId)) return;

        var message = new RemoveVolunteer(individualId, obj.What.BitCode);
        network.SendAll(message);
    }

    private void Handle_RemoveVolunteer(MessagePayload<RemoveVolunteer> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.IndividualId, out var individual)) return;

        individual.VolunteerTypes[obj.What.BitCode] = null;
    }

    private void Handle_VolunteersUpdated(MessagePayload<VolunteersUpdated> obj)
    {
        Dictionary<string, string[]> updatedVolunteerTypeIds = new();
        foreach (KeyValuePair<Hero, CharacterObject[]> keyValuePair in obj.What.UpdatedVolunteerTypes)
        {
            if (!objectManager.TryGetIdWithLogging(keyValuePair.Key, out var currentHeroId)) continue;

            updatedVolunteerTypeIds[currentHeroId] = new string[] {};
            foreach (CharacterObject character in keyValuePair.Value)
            {
                if (!objectManager.TryGetIdWithLogging(character, out var currentCharacterId)) continue;

                updatedVolunteerTypeIds[currentHeroId].AddItem(currentCharacterId);
            }
        }
        
        var message = new UpdateVolunteers(updatedVolunteerTypeIds);
        network.SendAll(message);
    }

    private void Handle_UpdateVolunteers(MessagePayload<UpdateVolunteers> obj)
    {
        Dictionary<Hero, CharacterObject[]> updatedVolunteerTypes = new();
        foreach (KeyValuePair<string, string[]> keyValuePair in obj.What.UpdatedVolunteerTypeIds)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(keyValuePair.Key, out var currentHero)) continue;

            for (int i = 0; i < keyValuePair.Value.Length; i++)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(keyValuePair.Value[i], out var currentCharacter)) continue;

                currentHero.VolunteerTypes[i] = currentCharacter;
            }
        }
    }
}