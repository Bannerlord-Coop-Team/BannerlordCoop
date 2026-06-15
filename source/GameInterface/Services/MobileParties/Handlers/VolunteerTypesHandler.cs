using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
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
        var individualId = obj.What.IndividualId;
        var bitCode = obj.What.BitCode;

        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(individualId, out var individual)) return;

                using (new AllowedThread())
                {
                    individual.VolunteerTypes[bitCode] = null;
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply RemoveVolunteer for hero ({individualId})", individualId);
            }
        });
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
                if (character is null) continue;

                if (!objectManager.TryGetIdWithLogging(character, out var currentCharacterId)) continue;

                updatedVolunteerTypeIds[currentHeroId].AddItem(currentCharacterId);
            }
        }
        
        var message = new UpdateVolunteers(updatedVolunteerTypeIds);
        network.SendAll(message);
    }

    private void Handle_UpdateVolunteers(MessagePayload<UpdateVolunteers> obj)
    {
        var updatedVolunteerTypeIds = obj.What.UpdatedVolunteerTypeIds;

        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                Dictionary<Hero, CharacterObject[]> updatedVolunteerTypes = new();
                foreach (KeyValuePair<string, string[]> keyValuePair in updatedVolunteerTypeIds)
                {
                    if (!objectManager.TryGetObjectWithLogging<Hero>(keyValuePair.Key, out var currentHero)) continue;

                    for (int i = 0; i < keyValuePair.Value.Length; i++)
                    {
                        if (!objectManager.TryGetObjectWithLogging<CharacterObject>(keyValuePair.Value[i], out var currentCharacter)) continue;

                        using (new AllowedThread())
                        {
                            currentHero.VolunteerTypes[i] = currentCharacter;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply UpdateVolunteers");
            }
        });
    }
}