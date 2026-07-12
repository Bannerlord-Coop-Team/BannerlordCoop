using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Handlers;

internal class VolunteerTypesHandler : IHandler
{
    private const string VolunteerSnapshotChannel = "VolunteerSnapshot";
    private const string VolunteerSnapshotInstance = "AllHeroes";

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer sendCoalescer;

    public VolunteerTypesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<VolunteerTypesArrayUpdated>(Handle_VolunteerTypesArrayUpdated);
        messageBroker.Subscribe<VolunteerRemoved>(Handle_VolunteerRemoved);
        messageBroker.Subscribe<RemoveVolunteer>(Handle_RemoveVolunteer);
        messageBroker.Subscribe<VolunteersUpdated>(Handle_VolunteersUpdated);
        messageBroker.Subscribe<UpdateVolunteers>(Handle_UpdateVolunteers);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VolunteerTypesArrayUpdated>(Handle_VolunteerTypesArrayUpdated);
        messageBroker.Unsubscribe<VolunteerRemoved>(Handle_VolunteerRemoved);
        messageBroker.Unsubscribe<RemoveVolunteer>(Handle_RemoveVolunteer);
        messageBroker.Unsubscribe<VolunteersUpdated>(Handle_VolunteersUpdated);
        messageBroker.Unsubscribe<UpdateVolunteers>(Handle_UpdateVolunteers);
    }

    private void Handle_VolunteerTypesArrayUpdated(MessagePayload<VolunteerTypesArrayUpdated> obj)
    {
        var data = obj.What;

        EnqueueSnapshot(data.Instance, data.Index, data.Value);
    }

    private void Handle_VolunteerRemoved(MessagePayload<VolunteerRemoved> obj)
    {
        EnqueueSnapshot(obj.What.Individual);
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
            if (!TrySerializeSnapshot(keyValuePair.Key, keyValuePair.Value, -1, null, out var heroId, out var volunteerTypeIds)) continue;

            updatedVolunteerTypeIds[heroId] = volunteerTypeIds;
        }

        EnqueueSnapshot(updatedVolunteerTypeIds);
    }

    private void EnqueueSnapshot(Hero hero, int changedIndex = -1, CharacterObject changedValue = null)
    {
        if (hero?.VolunteerTypes == null) return;
        if (!TrySerializeSnapshot(hero, hero.VolunteerTypes, changedIndex, changedValue, out var heroId, out var volunteerTypeIds)) return;

        EnqueueSnapshot(new Dictionary<string, string[]>
        {
            [heroId] = volunteerTypeIds,
        });
    }

    private void EnqueueSnapshot(Dictionary<string, string[]> snapshots)
    {
        if (snapshots.Count == 0) return;

        if (sendCoalescer == null)
        {
            network.SendAll(new UpdateVolunteers(snapshots));
            return;
        }

        sendCoalescer.Enqueue(
            new CoalesceKey(VolunteerSnapshotChannel, VolunteerSnapshotInstance),
            new VolunteerSnapshotPayload(snapshots));
    }

    private bool TrySerializeSnapshot(
        Hero hero,
        CharacterObject[] volunteerTypes,
        int changedIndex,
        CharacterObject changedValue,
        out string heroId,
        out string[] volunteerTypeIds)
    {
        heroId = null;
        volunteerTypeIds = null;
        if (!objectManager.TryGetIdWithLogging(hero, out heroId)) return false;

        heroId = Compact(heroId, typeof(Hero));
        volunteerTypeIds = new string[volunteerTypes.Length];
        for (int i = 0; i < volunteerTypes.Length; i++)
        {
            CharacterObject character = i == changedIndex ? changedValue : volunteerTypes[i];
            if (character != null && objectManager.TryGetIdWithLogging(character, out var characterId))
            {
                volunteerTypeIds[i] = Compact(characterId, typeof(CharacterObject));
            }
            else
            {
                volunteerTypeIds[i] = string.Empty;
            }
        }

        return true;
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
