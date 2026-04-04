using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Services.TroopRosters.Patches;
using SandBox.View.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

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
        messageBroker.Subscribe<ChangeTroopRostersAddToCountsAtIndex>(HandleAddToCountsAtIndex);
        messageBroker.Subscribe<ProccessRequestOnDoneRecruitmentVM>(HandleOnRecruitmentDone);
        messageBroker.Subscribe<ClientCloseRecruitmentVM>(Handle);
        messageBroker.Subscribe<ApproveChangeOnDoneRecruitmentVM>(Handle);
    }

    private void Handle(MessagePayload<ClientCloseRecruitmentVM> payload)
    {
        foreach (KeyValuePair<string, GameMenu> kvp in Campaign.Current.GameMenuManager._gameMenus)
        {
            Logger.Debug("GameMenu Key: {key}", kvp.Key);
        }
        //foreach (Tuple<IGauntletMovie, ViewModel> tuple in (ScreenManager.FocusedLayer as GauntletLayer).MoviesAndDataSources)
        //{
        //    if (tuple.Item2 is RecruitmentVM)
        //    {
        //        (tuple.Item2 as RecruitmentVM).Deactivate();
        //    }
        //}
    }


    // server process request
    public void HandleOnRecruitmentDone(MessagePayload<ProccessRequestOnDoneRecruitmentVM> payload)
    {
        var obj = payload.What;

        if (obj.TroopsInCart == null)
        {
            network.Send(obj.ClientWho, new ClientCloseRecruitmentVM());
            return;
        }

        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }
        using (new AllowedThread())
        {

            var troopsInCartList = obj.TroopsInCart.ToList();

            if (obj.TotalCost > mobileParty.LeaderHero.Gold)
            {
                // gold is not good respond to them for future ref reference.
                return;
            }

            List<(Hero, CharacterObject, int)> herosValidated = new();

            // validate they are all good before recruiting any
            foreach (var troop in troopsInCartList)
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

                if (volunteerTroopAtIndex is null)
                {
                    // later send decline for specific reason
                    return;
                }

                herosValidated.Add((hero, characterObject, troop.Item3));
            }

            foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
            {
                hero.VolunteerTypes[index] = null;
                mobileParty.MemberRoster.AddToCounts(characterObject, 1, false, 0, 0, true, -1);
                CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
            }

            GiveGoldAction.ApplyBetweenCharacters(mobileParty.LeaderHero, null, obj.TotalCost, true);
        var message = new ApproveChangeOnDoneRecruitmentVM(obj.MobilePartyId, obj.TroopsInCart, obj.TotalCost);

        network.Send(obj.ClientWho, new ClientCloseRecruitmentVM());

        network.SendAll(message);

        }
    }

    //client process approved recruitment
    private void Handle(MessagePayload<ApproveChangeOnDoneRecruitmentVM> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        using (new AllowedThread())
        {
            List<(Hero, CharacterObject, int)> herosValidated = new();

            // validate they are all good before recruiting any
            foreach (var troop in obj.TroopsInCart)
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

                if (volunteerTroopAtIndex is null)
                {
                    // later send decline for specific reason
                    return;
                }

                herosValidated.Add((hero, characterObject, troop.Item3));
            }

            foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
            {
                hero.VolunteerTypes[index] = null;
                mobileParty.MemberRoster.AddToCounts(characterObject, 1, false, 0, 0, true, -1);
                CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
            }

            GiveGoldAction.ApplyBetweenCharacters(mobileParty.LeaderHero, null, obj.Gold, true);
        }
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

    private void HandleAddToCountsAtIndex(MessagePayload<ChangeTroopRostersAddToCountsAtIndex> payload)
    {
        var obj = payload.What;
        if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty party) == false)
        {
            Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
            return;
        }

        AddToCountsTroopRosterPatch.RunAddToCountsAtIndex(party, obj.Index, obj.Count, obj.WoundedCount, obj.XpChanged, obj.RemoveDepleted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeTroopRostersAddToCounts>(HandleAddToCounts);
        messageBroker.Unsubscribe<ChangeTroopRostersAddToCountsAtIndex>(HandleAddToCountsAtIndex);
        messageBroker.Unsubscribe<ProccessRequestOnDoneRecruitmentVM>(HandleOnRecruitmentDone);
        messageBroker.Unsubscribe<ClientCloseRecruitmentVM>(Handle);
        messageBroker.Unsubscribe<ApproveChangeOnDoneRecruitmentVM>(Handle);
    }
}
