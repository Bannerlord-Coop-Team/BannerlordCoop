using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Services.TroopRosters.Patches;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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

        messageBroker.Subscribe<ChangeTroopRostersAddToCounts>(HandleAddToCounts);
        messageBroker.Subscribe<RecruitTroops>(HandleOnRecruitmentDone);
    }

    public void HandleOnRecruitmentDone(MessagePayload<RecruitTroops> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        List<(Hero, CharacterObject, int)> herosValidated = new();

        // validate they are all good before recruiting any
        foreach (var troop in obj.TroopsInCart)
        {
            if (objectManager.TryGetObject(troop.RecruiterHeroId, out Hero hero) == false)
            {
                Logger.Error("Unable to find Hero ({HeroId})", troop.RecruiterHeroId);
                continue;
            }

            if (objectManager.TryGetObject(troop.CharacterObjectId, out CharacterObject characterObject) == false)
            {
                Logger.Error("Unable to find CharacterObject ({CharacterObjectId})", troop.CharacterObjectId);
                continue;
            }


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

    private void HandleAddToCounts(MessagePayload<ChangeTroopRostersAddToCounts> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty party) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        if (objectManager.TryGetObject(obj.Character, out CharacterObject characterObject) == false)
        {
            Logger.Error("Unable to find CharacterObject ({characterObjectId})", obj.Character);
            return;
        }

        AddToCountsTroopRosterPatch.RunAddToCounts(
            party,
            characterObject,
            obj.Count,
            obj.InsertAtFront,
            obj.WoundedCount,
            obj.xpChanged,
            obj.RemoveDepleted,
            obj.Index);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeTroopRostersAddToCounts>(HandleAddToCounts);
        messageBroker.Unsubscribe<RecruitTroops>(HandleOnRecruitmentDone);
    }
}
