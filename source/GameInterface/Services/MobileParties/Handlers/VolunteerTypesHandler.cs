using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Handlers;

internal class VolunteerTypesHandler : IHandler
{
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
        var individualId = obj.What.IndividualId;
        var bitCode = obj.What.BitCode;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(individualId, out var individual)) return;

            using (new AllowedThread())
            {
                individual.VolunteerTypes[bitCode] = null;
            }
        }, context: $"RemoveVolunteer for hero ({individualId})");
    }

    private void Handle_VolunteersUpdated(MessagePayload<VolunteersUpdated> obj)
    {
        Dictionary<string, string[]> updatedVolunteerTypeIds = new();
        foreach (KeyValuePair<Hero, CharacterObject[]> keyValuePair in obj.What.UpdatedVolunteerTypes)
        {
            if (!objectManager.TryGetIdWithLogging(keyValuePair.Key, out var currentHeroId)) continue;
            currentHeroId = Compact(currentHeroId, typeof(Hero));

            CharacterObject[] volunteerTypes = keyValuePair.Value;
            string[] volunteerTypeIds = new string[volunteerTypes.Length];
            for (int i = 0; i < volunteerTypes.Length; i++)
            {
                CharacterObject character = volunteerTypes[i];
                if (character != null && objectManager.TryGetIdWithLogging(character, out var currentCharacterId))
                {
                    volunteerTypeIds[i] = Compact(currentCharacterId, typeof(CharacterObject));
                }
                else
                {
                    volunteerTypeIds[i] = string.Empty;
                }
            }
            updatedVolunteerTypeIds[currentHeroId] = volunteerTypeIds;
        }
        
        var message = new UpdateVolunteers(updatedVolunteerTypeIds);
        network.SendAll(message);
    }

    private void Handle_UpdateVolunteers(MessagePayload<UpdateVolunteers> obj)
    {
        var updatedVolunteerTypeIds = obj.What.UpdatedVolunteerTypeIds;

        GameThread.RunSafe(() =>
        {
            foreach (KeyValuePair<string, string[]> keyValuePair in updatedVolunteerTypeIds)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(keyValuePair.Key, out var currentHero)) continue;

                string[] volunteerTypeIds = keyValuePair.Value;
                using (new AllowedThread())
                {
                    for (int i = 0; i < volunteerTypeIds.Length && i < currentHero.VolunteerTypes.Length; i++)
                    {
                        if (string.IsNullOrEmpty(volunteerTypeIds[i]))
                        {
                            currentHero.VolunteerTypes[i] = null;
                        }
                        else if (objectManager.TryGetObjectWithLogging<CharacterObject>(volunteerTypeIds[i], out var currentCharacter))
                        {
                            currentHero.VolunteerTypes[i] = currentCharacter;
                        }
                    }
                }
            }
        }, context: "UpdateVolunteers");
    }
}