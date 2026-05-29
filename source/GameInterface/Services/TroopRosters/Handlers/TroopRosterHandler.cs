using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Services.TroopRosters.Patches;
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

        
        messageBroker.Subscribe<ChangeTroopRostersAddToCounts>(Handle_AddToCounts);
        messageBroker.Subscribe<ChangeTroopRostersAddToCountsAtIndex>(Handle_AddToCountsAtIndex);
        messageBroker.Subscribe<ChangeTroopRostersHeroAddToCounts>(Handle_HeroAddToCounts);
        messageBroker.Subscribe<RecruitTroops>(HandleOnRecruitmentDone);

        messageBroker.Subscribe<TroopRemoved>(Handle_TroopRemoved);
        messageBroker.Subscribe<NetworkRemoveTroop>(Handle_NetworkRemoveTroop);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeTroopRostersAddToCounts>(Handle_AddToCounts);
        messageBroker.Unsubscribe<ChangeTroopRostersAddToCountsAtIndex>(Handle_AddToCountsAtIndex);
        messageBroker.Unsubscribe<RecruitTroops>(HandleOnRecruitmentDone);
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

    private void Handle_AddToCountsAtIndex(MessagePayload<ChangeTroopRostersAddToCountsAtIndex> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging(obj.TroopRosterId, out TroopRoster troopRoster)) return;

        AddToCountsTroopRosterPatch.RunAddToCountsAtIndex(
            troopRoster,
            obj.Index,
            obj.Count,
            obj.WoundedCount,
            obj.XpChanged,
            obj.RemoveDepleted);
    }

    private void Handle_AddToCounts(MessagePayload<ChangeTroopRostersAddToCounts> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging(obj.TroopRosterId, out TroopRoster troopRoster)) return;
        if (!objectManager.TryGetObjectWithLogging(obj.Character, out CharacterObject characterObject)) return;

        AddToCountsTroopRosterPatch.RunAddToCounts(
            troopRoster,
            characterObject,
            obj.Count,
            obj.InsertAtFront,
            obj.WoundedCount,
            obj.xpChanged,
            obj.RemoveDepleted,
            obj.Index);
    }

    private void Handle_HeroAddToCounts(MessagePayload<ChangeTroopRostersHeroAddToCounts> payload)
    {
        var obj = payload.What;
        if (!objectManager.TryGetObjectWithLogging(obj.TroopRosterId, out TroopRoster troopRoster)) return;
        if (!objectManager.TryGetObjectWithLogging(obj.HeroId, out Hero hero)) return;

        AddToCountsTroopRosterPatch.RunAddToCounts(
            troopRoster,
            hero.CharacterObject,
            obj.Count,
            obj.InsertAtFront,
            obj.WoundedCount,
            obj.XpChanged,
            obj.RemoveDepleted,
            obj.Index);
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

        try
        {
            using (new AllowedThread())
            {
                troopRoster.RemoveTroop(troop, payload.What.NumberToRemove, xp: payload.What.Xp);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while removing troop from roster");
        }
    }
}