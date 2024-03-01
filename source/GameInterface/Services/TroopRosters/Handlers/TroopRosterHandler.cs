using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Services.TroopRosters.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.TroopRosters.Handlers;
public class TroopRosterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public TroopRosterHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ChangeTroopRostersAddToCounts>(HandleAddToCounts);

        messageBroker.Subscribe<ProccessRequestOnDoneRecruitmentVM>(HandleOnRecruitmentDone);
    }


    // server process request
    private void HandleOnRecruitmentDone(MessagePayload<ProccessRequestOnDoneRecruitmentVM> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        var troopsInCartList = obj.TroopsInCart.ToList();


        int num = troopsInCartList.Sum(troop => troop.Item3);

        if(num > mobileParty.LeaderHero.Gold)
        {
            // gold is not good respond to them for future ref reference.
            return;
        }

        List<(Hero, CharacterObject, int)> herosValidated = new();

        // validate they are all good before recruiting any
        foreach(var troop in troopsInCartList)
        {
            if (objectManager.TryGetObject(troop.Item1, out Hero hero) == false)
            {
                Logger.Error("Unable to find Hero ({HeroId})", troop.Item1);
                // send decline to them at some point...
                return;
            }

            if (objectManager.TryGetObject(troop.Item2, out CharacterObject characterObject) == false)
            {
                Logger.Error("Unable to find Hero ({CharacterObjectId})", troop.Item2);
                // send decline to them at some point...
                return;
            }


            var volunteerTroopAtIndex = hero.VolunteerTypes[troop.Item3];

            if(volunteerTroopAtIndex is null)
            {
                // later send decline for specific reason
                return;
            }

            herosValidated.Add((hero, characterObject, troop.Item3));
        }

        foreach((Hero hero, CharacterObject characterObject, int index) in herosValidated)
        {
            hero.VolunteerTypes[index] = null;
            mobileParty.MemberRoster.AddToCounts(characterObject, 1, false, 0, 0, true, -1);
            CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
        }

        GiveGoldAction.ApplyBetweenCharacters(mobileParty.LeaderHero, null, num, true);

        // message to specific client then other clients?
        var message = new ApproveChangeOnDoneRecruitmentVM(obj.MobilePartyId, obj.TroopsInCart, num);


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

        // TODO: TEST AND VERIFY WORKS

        AddToCountsTroopRosterPatch.RunAddToCounts(party, characterObject, obj.Count, obj.InsertAtFront, obj.WoundedCount, obj.xpChanged, obj.RemoveDepleted, obj.Index);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeTroopRostersAddToCounts>(HandleAddToCounts);
        messageBroker.Unsubscribe<ProccessRequestOnDoneRecruitmentVM>(HandleOnRecruitmentDone);


    }
}
