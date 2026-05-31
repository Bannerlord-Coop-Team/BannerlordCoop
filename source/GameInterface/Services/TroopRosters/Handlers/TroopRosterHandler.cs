using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers;

public class TroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public TroopRosterHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<TroopRosterAddToCountsChanged>(Handle_AddToCountsChanged);
        messageBroker.Subscribe<NetworkChangeTroopRosterAddToCounts>(Handle_AddToCounts);
        messageBroker.Subscribe<RecruitTroops>(HandleOnRecruitmentDone);

        messageBroker.Subscribe<TroopRemoved>(Handle_TroopRemoved);
        messageBroker.Subscribe<NetworkRemoveTroop>(Handle_NetworkRemoveTroop);

        messageBroker.Subscribe<TroopRosterTroopWounded>(Handle_TroopWounded);
        messageBroker.Subscribe<NetworkTroopRosterWoundTroop>(Handle_NetworkWoundTroop);

        messageBroker.Subscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);
        messageBroker.Subscribe<NetworkRemoveZeroCounts>(Handle_NetworkRemoveZeroCounts);

        messageBroker.Subscribe<XpAtTroopIndexAdded>(Handle_XpAtTroopIndexAdded);
        messageBroker.Subscribe<NetworkAddXpToTroopIndex>(Handle_NetworkAddXpToTroopIndex);

        messageBroker.Subscribe<TroopRosterCleared>(Handle_TroopRosterCleared);
        messageBroker.Subscribe<NetworkClearTroopRoster>(Handle_NetworkClearTroopRoster);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TroopRosterAddToCountsChanged>(Handle_AddToCountsChanged);
        messageBroker.Unsubscribe<NetworkChangeTroopRosterAddToCounts>(Handle_AddToCounts);
        messageBroker.Unsubscribe<RecruitTroops>(HandleOnRecruitmentDone);

        messageBroker.Unsubscribe<TroopRemoved>(Handle_TroopRemoved);
        messageBroker.Unsubscribe<NetworkRemoveTroop>(Handle_NetworkRemoveTroop);

        messageBroker.Unsubscribe<TroopRosterTroopWounded>(Handle_TroopWounded);
        messageBroker.Unsubscribe<NetworkTroopRosterWoundTroop>(Handle_NetworkWoundTroop);

        messageBroker.Unsubscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);
        messageBroker.Unsubscribe<NetworkRemoveZeroCounts>(Handle_NetworkRemoveZeroCounts);

        messageBroker.Unsubscribe<XpAtTroopIndexAdded>(Handle_XpAtTroopIndexAdded);
        messageBroker.Unsubscribe<NetworkAddXpToTroopIndex>(Handle_NetworkAddXpToTroopIndex);

        messageBroker.Unsubscribe<TroopRosterCleared>(Handle_TroopRosterCleared);
        messageBroker.Unsubscribe<NetworkClearTroopRoster>(Handle_NetworkClearTroopRoster);
    }

    public void HandleOnRecruitmentDone(MessagePayload<RecruitTroops> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging(obj.MobilePartyId, out MobileParty mobileParty)) return;

        List<(Hero, CharacterObject, int)> herosValidated = new();

        // validate they are all good before recruiting any
        foreach (var troop in obj.TroopsInCart)
        {
            if (!objectManager.TryGetObjectWithLogging(troop.RecruiterHeroId, out Hero hero)) continue;
            if (!objectManager.TryGetObjectWithLogging(troop.CharacterObjectId, out CharacterObject characterObject)) continue;


            var volunteerTroopAtIndex = hero.VolunteerTypes[troop.TroopIndex];

            if (volunteerTroopAtIndex is null)
            {
                // later send decline for specific reason
                continue;
            }

            herosValidated.Add((hero, characterObject, troop.TroopIndex));
        }

        // Calculate cost before changing any data
        var cost = 0;
        foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
        {
            cost += Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(characterObject, mobileParty.LeaderHero).RoundedResultNumber;
        }

        // Do not apply recruitment if the player does not have enough gold
        if (cost > mobileParty.LeaderHero.Gold)
        {
            Logger.Warning("Attempted to recruit troops that cost more than the player had");
            return;
        }

        // Commit recruitment
        foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
        {
            hero.VolunteerTypes[index] = null;
            messageBroker.Publish(this, new VolunteerTypesArrayUpdated(hero, null, index));

            mobileParty.MemberRoster.AddToCounts(characterObject, 1, false, 0, 0, true, -1);
            CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
        }

        mobileParty.LeaderHero.Gold -= cost;
    }

    private void Handle_AddToCountsChanged(MessagePayload<TroopRosterAddToCountsChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.TroopRoster, out var troopRosterId)) return;

        var objectToResolve = obj.Troop.IsHero
            ? (object)obj.Troop.HeroObject
            : obj.Troop;

        if (!objectManager.TryGetIdWithLogging(objectToResolve, out var objectId)) return;

        if (TroopRosterConfig.Debug)
        {
            Logger.Debug("[{Instance}] Sending troop roster add to counts change for " +
                "TroopRoster {TroopRosterId}, " +
                "CharacterObject {CharacterObjectId}, " +
                "IsHero {IsHero}, " +
                "Count {Count}, " +
                "InsertAtFront {InsertAtFront}, " +
                "WoundedCount {WoundedCount}, " +
                "XpChanged {XpChanged}, " +
                "RemoveDepleted {RemoveDepleted}, " +
                "Index {Index}, " +
                "StackTrace {StackTrace}",
                ModInformation.IsServer ? "Server" : "Client",
                troopRosterId,
                objectId,
                obj.Troop.IsHero,
                obj.Count,
                obj.InsertAtFront,
                obj.WoundedCount,
                obj.XpChanged,
                obj.RemoveDepleted,
                obj.Index,
                Environment.StackTrace);
        }

        var message = new NetworkChangeTroopRosterAddToCounts(
            troopRosterId,
            objectId,
            obj.Troop.IsHero,
            obj.Count,
            obj.InsertAtFront,
            obj.WoundedCount,
            obj.XpChanged,
            obj.RemoveDepleted,
            obj.Index);

        network.SendAll(message);
    }

    private void Handle_AddToCounts(MessagePayload<NetworkChangeTroopRosterAddToCounts> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging(obj.TroopRosterId, out TroopRoster troopRoster))
            return;
        if (!TryResolveCharacterObject(obj.ObjectId, obj.IsHero, out var characterObject))
            return;


        if (obj.WoundedCount < 0)
        {
            Logger.Error("Wounded count change cannot be negative. " +
                "TroopRosterId: {TroopRosterId}, " +
                "Character: {character}, " +
                "Count: {count}, " +
                "WoundedCount: {woundedCount}",
                obj.TroopRosterId,
                characterObject.Name,
                obj.Count,
                obj.WoundedCount);
            return;
        }

        using (new AllowedThread())
        {
            try
            {
                if (TroopRosterConfig.Debug)
                {
                    Logger.Debug("[{Instance}] Setting troop roster counts for " +
                        "TroopRosterId: {TroopRosterId}, " +
                        "CharacterId: {CharacterId}", 
                        ModInformation.IsServer ? "Server" : "Client",
                        obj.TroopRosterId,
                        obj.ObjectId);
                }

                troopRoster.AddToCounts(characterObject, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.XpChanged, obj.RemoveDepleted, obj.Index);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to AddToCountsAtIndex. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }

    private bool TryResolveCharacterObject(string objectId, bool isHero, out CharacterObject characterObject)
    {
        characterObject = null;

        if (isHero)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(objectId, out var hero))
                return false;

            characterObject = hero.CharacterObject;
            return characterObject != null;
        }

        return objectManager.TryGetObjectWithLogging<CharacterObject>(
            objectId,
            out characterObject);
    }

    private void Handle_TroopRemoved(MessagePayload<TroopRemoved> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.TroopRoster, out var troopRosterId))
            return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Troop, out var characterObjectId)) 
           return;

        var message = new NetworkRemoveTroop(
            troopRosterId,
            characterObjectId,
            payload.What.NumberToRemove,
            payload.What.Xp);

        network.SendAll(message);
    }

    private void Handle_NetworkRemoveTroop(MessagePayload<NetworkRemoveTroop> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.TroopRosterId, out TroopRoster troopRoster))
            return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.TroopId, out CharacterObject troop))
            return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.RemoveTroop(troop, payload.What.NumberToRemove, xp: payload.What.Xp);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while removing troop from roster, TroopRosterId: {TroopRosterId}", payload.What.TroopRosterId);
            }
        }
    }

    private void Handle_TroopWounded(MessagePayload<TroopRosterTroopWounded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.TroopRoster, out var troopRosterId))
            return;

        var objectToResolve = obj.Troop.IsHero
            ? (object)obj.Troop.HeroObject
            : obj.Troop;

        if (!objectManager.TryGetIdWithLogging(objectToResolve, out var objectId))
            return;

        var message = new NetworkTroopRosterWoundTroop(troopRosterId, objectId, obj.Troop.IsHero, obj.NumberToWound);
        network.SendAll(message);
    }

    private void Handle_NetworkWoundTroop(MessagePayload<NetworkTroopRosterWoundTroop> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.TroopRosterId, out var troopRoster))
            return;

        if (!TryResolveCharacterObject(obj.ObjectId, obj.IsHero, out var characterObject))
            return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.WoundTroop(characterObject, obj.NumberToWound);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to WoundTroop. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }

    private void Handle_ZeroCountsRemoved(MessagePayload<ZeroCountsRemoved> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.TroopRoster, out var troopRosterId)) return;

        var message = new NetworkRemoveZeroCounts(troopRosterId);
        network.SendAll(message);
    }

    private void Handle_NetworkRemoveZeroCounts(MessagePayload<NetworkRemoveZeroCounts> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(payload.What.TroopRosterId, out var troopRoster)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.RemoveZeroCounts();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to RemoveZeroCounts. TroopRosterId: {TroopRosterId}", payload.What.TroopRosterId);
            }
        }
    }

    private void Handle_XpAtTroopIndexAdded(MessagePayload<XpAtTroopIndexAdded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(payload.What.TroopRoster, out var troopRosterId)) return;

        var message = new NetworkAddXpToTroopIndex(troopRosterId, obj.Index, obj.XpAmount);
        network.SendAll(message);
    }

    private void Handle_NetworkAddXpToTroopIndex(MessagePayload<NetworkAddXpToTroopIndex> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.AddXpToTroopAtIndex(obj.Index, obj.XpAmount);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to RemoveZeroCounts. TroopRosterId: {TroopRosterId}", payload.What.TroopRosterId);
            }
        }
    }

    private void Handle_TroopRosterCleared(MessagePayload<TroopRosterCleared> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(payload.What.TroopRoster, out var troopRosterId)) return;

        var message = new NetworkClearTroopRoster(troopRosterId);
        network.SendAll(message);
    }

    private void Handle_NetworkClearTroopRoster(MessagePayload<NetworkClearTroopRoster> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;

        try
        {
            using (new AllowedThread())
            {
                troopRoster.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to Clear. TroopRosterId: {TroopRosterId}", payload.What.TroopRosterId);
        }
    }
}