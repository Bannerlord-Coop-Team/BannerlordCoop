using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
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
        messageBroker.Subscribe<VolunteersUpdated>(Handle_VolunteersUpdated);
        messageBroker.Subscribe<UpdateVolunteers>(Handle_UpdateVolunteers);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VolunteerTypesArrayUpdated>(Handle_VolunteerTypesArrayUpdated);
        messageBroker.Unsubscribe<VolunteerRemoved>(Handle_VolunteerRemoved);
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

    private void Handle_VolunteersUpdated(MessagePayload<VolunteersUpdated> obj)
    {
        Dictionary<uint, uint[]> updatedVolunteerTypeIds = new();
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

        EnqueueSnapshot(new Dictionary<uint, uint[]>
        {
            [heroId] = volunteerTypeIds,
        });
    }

    private void EnqueueSnapshot(Dictionary<uint, uint[]> snapshots)
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
        out uint heroId,
        out uint[] volunteerTypeIds)
    {
        heroId = 0;
        volunteerTypeIds = null;
        if (!objectManager.TryGetHandleWithLogging(hero, out heroId)) return false;

        volunteerTypeIds = new uint[volunteerTypes.Length];
        for (int i = 0; i < volunteerTypes.Length; i++)
        {
            CharacterObject character = i == changedIndex ? changedValue : volunteerTypes[i];
            if (character == null)
            {
                volunteerTypeIds[i] = 0;
                continue;
            }

            if (!objectManager.TryGetHandleWithLogging(character, out var characterId)) return false;

            volunteerTypeIds[i] = characterId;
        }

        return true;
    }

    private void Handle_UpdateVolunteers(MessagePayload<UpdateVolunteers> obj)
    {
        var updatedVolunteerTypeIds = obj.What.UpdatedVolunteerTypeIds;

        GameThread.RunSafe(() =>
        {
            foreach (KeyValuePair<uint, uint[]> keyValuePair in updatedVolunteerTypeIds)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(keyValuePair.Key, out var currentHero)) continue;

                uint[] volunteerTypeIds = keyValuePair.Value;
                using (new AllowedThread())
                {
                    for (int i = 0; i < volunteerTypeIds.Length && i < currentHero.VolunteerTypes.Length; i++)
                    {
                        if (volunteerTypeIds[i] == 0)
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
