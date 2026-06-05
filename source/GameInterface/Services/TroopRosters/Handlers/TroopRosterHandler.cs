using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
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


        messageBroker.Subscribe<CountsAtIndexAdded>(Handle_CountsAtIndexAdded);
        messageBroker.Subscribe<NetworkAddToCountsAtIndex>(Handle_NetworkAddToCountsAtIndex);

        messageBroker.Subscribe<NewElementAdded>(Handle_NewElementAdded);
        messageBroker.Subscribe<NetworkAddNewElement>(Handle_NetworkAddNewElement);

        messageBroker.Subscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);
        messageBroker.Subscribe<NetworkRemoveZeroCounts>(Handle_NetworkRemoveZeroCounts);

        messageBroker.Subscribe<ElementNumberSet>(Handle_ElementNumberSet);
        messageBroker.Subscribe<NetworkSetElementNumber>(Handle_NetworkSetElementNumber);

        messageBroker.Subscribe<ElementWoundedNumberSet>(Handle_ElementWoundedNumberSet);
        messageBroker.Subscribe<NetworkSetElementWoundedNumber>(Handle_NetworkSetElementWoundedNumber);

        messageBroker.Subscribe<ElementXpSet>(Handle_ElementXpSet);
        messageBroker.Subscribe<NetworkSetElementXp>(Handle_NetworkSetElementXp);

        messageBroker.Subscribe<TroopShiftedToIndex>(Handle_TroopShiftedToIndex);
        messageBroker.Subscribe<NetworkShiftTroopToIndex>(Handle_NetworkShiftTroopToIndex);

        messageBroker.Subscribe<TroopsSwappedAtIndices>(Handle_TroopsSwappedAtIndices);
        messageBroker.Subscribe<NetworkSwapTroopsAtIndices>(Handle_NetworkSwapTroopsAtIndices);
    }

    public void Dispose()
    {

        messageBroker.Unsubscribe<CountsAtIndexAdded>(Handle_CountsAtIndexAdded);
        messageBroker.Unsubscribe<NetworkAddToCountsAtIndex>(Handle_NetworkAddToCountsAtIndex);

        messageBroker.Unsubscribe<NewElementAdded>(Handle_NewElementAdded);
        messageBroker.Unsubscribe<NetworkAddNewElement>(Handle_NetworkAddNewElement);

        messageBroker.Unsubscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);
        messageBroker.Unsubscribe<NetworkRemoveZeroCounts>(Handle_NetworkRemoveZeroCounts);

        messageBroker.Unsubscribe<ElementNumberSet>(Handle_ElementNumberSet);
        messageBroker.Unsubscribe<NetworkSetElementNumber>(Handle_NetworkSetElementNumber);

        messageBroker.Unsubscribe<ElementWoundedNumberSet>(Handle_ElementWoundedNumberSet);
        messageBroker.Unsubscribe<NetworkSetElementWoundedNumber>(Handle_NetworkSetElementWoundedNumber);

        messageBroker.Unsubscribe<ElementXpSet>(Handle_ElementXpSet);
        messageBroker.Unsubscribe<NetworkSetElementXp>(Handle_NetworkSetElementXp);

        messageBroker.Unsubscribe<TroopShiftedToIndex>(Handle_TroopShiftedToIndex);
        messageBroker.Unsubscribe<NetworkShiftTroopToIndex>(Handle_NetworkShiftTroopToIndex);

        messageBroker.Unsubscribe<TroopsSwappedAtIndices>(Handle_TroopsSwappedAtIndices);
        messageBroker.Unsubscribe<NetworkSwapTroopsAtIndices>(Handle_NetworkSwapTroopsAtIndices);
    }

    private bool TryResolveCharacterObject(string objectId, bool isHero, out CharacterObject characterObject)
    {
        characterObject = null;

        if (isHero)
        {
            if (!objectManager.TryGetObject<Hero>(objectId, out var hero))
                return false;

            characterObject = hero.CharacterObject;
            return characterObject != null;
        }

        return objectManager.TryGetObject(objectId, out characterObject);
    }

    private bool TryGetCharacterId(CharacterObject character, out string objectId, out bool isHero)
    {
        objectId = null;
        isHero = character != null && character.IsHero;

        var objectToResolve = isHero ? (object)character.HeroObject : character;
        return objectManager.TryGetId(objectToResolve, out objectId);
    }

    /// <summary>
    /// Resolves the index of the element with the given character within the roster on the
    /// receiving side. The element is keyed by character rather than a raw index so positional
    /// differences between the authority and this instance do not corrupt the wrong slot.
    /// </summary>
    private bool TryResolveTroopIndex(TroopRoster troopRoster, string objectId, bool isHero, out int index)
    {
        index = -1;
        if (!TryResolveCharacterObject(objectId, isHero, out var character)) return false;

        index = troopRoster.FindIndexOfTroop(character);
        if (index < 0)
        {
            Logger.Error("Could not find troop {ObjectId} in roster to resolve its index", objectId);
            return false;
        }

        return true;
    }

    #region AddToCountsAtIndex
    private void Handle_CountsAtIndexAdded(MessagePayload<CountsAtIndexAdded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;
        if (!TryGetCharacterId(obj.Character, out var objectId, out var isHero)) return;

        network.SendAll(new NetworkAddToCountsAtIndex(
            troopRosterId,
            objectId,
            isHero,
            obj.CountChange,
            obj.WoundedCountChange,
            obj.XpChange,
            obj.RemoveDepleted));
    }

    private void Handle_NetworkAddToCountsAtIndex(MessagePayload<NetworkAddToCountsAtIndex> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject(obj.TroopRosterId, out TroopRoster troopRoster)) return;
        if (!TryResolveTroopIndex(troopRoster, obj.ObjectId, obj.IsHero, out var index)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.AddToCountsAtIndex(index, obj.CountChange, obj.WoundedCountChange, obj.XpChange, obj.RemoveDepleted);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to AddToCountsAtIndex. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion

    #region AddNewElement
    private void Handle_NewElementAdded(MessagePayload<NewElementAdded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;
        if (!TryGetCharacterId(obj.Character, out var objectId, out var isHero)) return;

        network.SendAll(new NetworkAddNewElement(troopRosterId, objectId, isHero, obj.InsertionIndex));
    }

    private void Handle_NetworkAddNewElement(MessagePayload<NetworkAddNewElement> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject(obj.TroopRosterId, out TroopRoster troopRoster)) return;
        if (!TryResolveCharacterObject(obj.ObjectId, obj.IsHero, out var characterObject)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.AddNewElement(characterObject, obj.InsertionIndex);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to AddNewElement. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion

    #region RemoveZeroCounts
    private void Handle_ZeroCountsRemoved(MessagePayload<ZeroCountsRemoved> payload)
    {
        if (!objectManager.TryGetId(payload.What.TroopRoster, out var troopRosterId)) return;

        network.SendAll(new NetworkRemoveZeroCounts(troopRosterId));
    }

    private void Handle_NetworkRemoveZeroCounts(MessagePayload<NetworkRemoveZeroCounts> payload)
    {
        if (!objectManager.TryGetObject<TroopRoster>(payload.What.TroopRosterId, out var troopRoster)) return;

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
    #endregion

    #region SetElementNumber
    private void Handle_ElementNumberSet(MessagePayload<ElementNumberSet> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;
        if (!TryGetCharacterId(obj.Character, out var objectId, out var isHero)) return;

        network.SendAll(new NetworkSetElementNumber(troopRosterId, objectId, isHero, obj.Number));
    }

    private void Handle_NetworkSetElementNumber(MessagePayload<NetworkSetElementNumber> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;
        if (!TryResolveTroopIndex(troopRoster, obj.ObjectId, obj.IsHero, out var index)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.SetElementNumber(index, obj.Number);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to SetElementNumber. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion

    #region SetElementWoundedNumber
    private void Handle_ElementWoundedNumberSet(MessagePayload<ElementWoundedNumberSet> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;
        if (!TryGetCharacterId(obj.Character, out var objectId, out var isHero)) return;

        network.SendAll(new NetworkSetElementWoundedNumber(troopRosterId, objectId, isHero, obj.Number));
    }

    private void Handle_NetworkSetElementWoundedNumber(MessagePayload<NetworkSetElementWoundedNumber> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;
        if (!TryResolveTroopIndex(troopRoster, obj.ObjectId, obj.IsHero, out var index)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.SetElementWoundedNumber(index, obj.Number);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to SetElementWoundedNumber. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion

    #region SetElementXp
    private void Handle_ElementXpSet(MessagePayload<ElementXpSet> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;
        if (!TryGetCharacterId(obj.Character, out var objectId, out var isHero)) return;

        network.SendAll(new NetworkSetElementXp(troopRosterId, objectId, isHero, obj.Number));
    }

    private void Handle_NetworkSetElementXp(MessagePayload<NetworkSetElementXp> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;
        if (!TryResolveTroopIndex(troopRoster, obj.ObjectId, obj.IsHero, out var index)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.SetElementXp(index, obj.Number);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to SetElementXp. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion

    #region ShiftTroopToIndex
    private void Handle_TroopShiftedToIndex(MessagePayload<TroopShiftedToIndex> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;

        network.SendAll(new NetworkShiftTroopToIndex(troopRosterId, obj.TroopIndex, obj.TargetIndex));
    }

    private void Handle_NetworkShiftTroopToIndex(MessagePayload<NetworkShiftTroopToIndex> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.ShiftTroopToIndex(obj.TroopIndex, obj.TargetIndex);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to ShiftTroopToIndex. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion

    #region SwapTroopsAtIndices
    private void Handle_TroopsSwappedAtIndices(MessagePayload<TroopsSwappedAtIndices> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetId(obj.TroopRoster, out var troopRosterId)) return;

        network.SendAll(new NetworkSwapTroopsAtIndices(troopRosterId, obj.FirstIndex, obj.SecondIndex));
    }

    private void Handle_NetworkSwapTroopsAtIndices(MessagePayload<NetworkSwapTroopsAtIndices> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetObject<TroopRoster>(obj.TroopRosterId, out var troopRoster)) return;

        using (new AllowedThread())
        {
            try
            {
                troopRoster.SwapTroopsAtIndices(obj.FirstIndex, obj.SecondIndex);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to SwapTroopsAtIndices. TroopRosterId: {TroopRosterId}", obj.TroopRosterId);
            }
        }
    }
    #endregion
}