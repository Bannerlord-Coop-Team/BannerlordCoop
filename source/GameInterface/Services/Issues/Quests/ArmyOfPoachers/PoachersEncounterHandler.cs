using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

internal class PoachersEncounterHandler : IHandler
{
    private readonly IMessageBroker messages;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IIssueOwnerResolver owners;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;
    private readonly HashSet<Quest> pendingBattles = new HashSet<Quest>();

    public PoachersEncounterHandler(IMessageBroker messages, INetwork network, IObjectManager objects,
        IIssueResolver issues, IIssueOwnerResolver owners, IControllerIdProvider controller, IPlayerManager players)
    {
        this.messages = messages;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.owners = owners;
        this.controller = controller;
        this.players = players;
        messages.Subscribe<RequestPoachersParty>(OnPartyRequested);
        messages.Subscribe<NetworkPoachersPartyCreated>(OnPartyCreated);
    }

    public void Dispose()
    {
        messages.Unsubscribe<RequestPoachersParty>(OnPartyRequested);
        messages.Unsubscribe<NetworkPoachersPartyCreated>(OnPartyCreated);
        pendingBattles.Clear();
    }

    public bool IsLocalOwner(Quest quest)
    {
        var issue = quest.QuestGiver.Issue;
        return issue?.IssueQuest == quest && owners.TryGetOwnerControllerId(issue, out var owner) && owner == controller.ControllerId;
    }

    public bool IsVillageBattle(MobileParty attacker, MobileParty defender)
    {
        return Campaign.Current.IssueManager.Issues.Values.Any(issue =>
            issue.IssueQuest is Quest quest && quest.IsOngoing && !quest._isReadyToBeFinalized &&
            owners.TryResolveOwnerHero(issue, out var hero) && !hero.IsWounded &&
            ((quest._poachersParty == defender && hero.PartyBelongedTo == attacker) ||
             (quest._poachersParty == attacker && hero.PartyBelongedTo == defender)) &&
            attacker.CurrentSettlement == quest._questVillage.Settlement && defender.CurrentSettlement == attacker.CurrentSettlement);
    }

    public void RequestParty(Quest quest)
    {
        if (ModInformation.IsServer || quest._poachersParty != null || !IsLocalOwner(quest) ||
            !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;

        network.SendAll(new RequestPoachersParty(giverId, quest.QuestGiver.Issue.StringId));
    }

    private void OnPartyRequested(MessagePayload<RequestPoachersParty> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;
        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!TryGetQuest(request.IssueOwnerId, request.IssueId, out var quest) ||
                !players.TryGetPlayer(peer, out var player) ||
                !owners.TryGetOwnerControllerId(quest.QuestGiver.Issue, out var ownerId) || ownerId != player.ControllerId ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

            if (quest._poachersParty == null)
            {
                if (party.CurrentSettlement != quest._questVillage.Settlement || hero.IsWounded ||
                    !CampaignTime.Now.IsNightTime || quest._isReadyToBeFinalized ||
                    SettlementHelper.FindNearestHideoutToMobileParty(party, MobileParty.NavigationType.Default,
                        settlement => settlement.IsActive) == null) return;

                using (new MainHeroSubstitutionScope(hero, party))
                {
                    quest.CreatePoachersParty();
                }
            }

            if (!objects.TryGetIdWithLogging(quest._poachersParty, out var partyId)) return;
            network.SendAll(new NetworkPoachersPartyCreated(request.IssueOwnerId, request.IssueId, ownerId, partyId));
        }, context: nameof(PoachersEncounterHandler));
    }

    private void OnPartyCreated(MessagePayload<NetworkPoachersPartyCreated> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        if (data.ControllerId != controller.ControllerId) return;

        GameThread.RunSafe(() =>
        {
            if (!TryGetQuest(data.IssueOwnerId, data.IssueId, out var quest) ||
                !objects.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var party)) return;

            quest._poachersParty = party;
            var menu = Campaign.Current.CurrentMenuContext;
            if (menu?.GameMenu.StringId == "army_of_poachers_village")
                Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(menu);
        }, context: nameof(PoachersEncounterHandler));
    }

    public void StartBattle(Quest quest)
    {
        if (ModInformation.IsServer || !quest.IsOngoing || !IsLocalOwner(quest) ||
            quest._poachersParty == null || Hero.MainHero.IsWounded || !pendingBattles.Add(quest)) return;

        try
        {
            messages.Publish(this, new ConversationRequested(PartyBase.MainParty, quest._poachersParty.Party, false,
                ConversationRestartSource.PlayerEncounter, false, accepted =>
                {
                    try
                    {
                        if (!accepted || !quest.IsOngoing || !IsLocalOwner(quest) || PlayerEncounter.Current == null ||
                            PlayerEncounter.EncounteredParty != quest._poachersParty?.Party) return;

                        PlayerEncounter.StartBattle();
                        var battle = MobileParty.MainParty.MapEvent;
                        if (battle == null || battle != quest._poachersParty.MapEvent) return;
                        PlayerEncounter.Update();
                        quest._talkedToPoachersBattleWillStart = false;
                        quest._isReadyToBeFinalized = true;
                        GameMenu.ActivateGameMenu("army_of_poachers_village");
                        if (!objects.TryGetIdWithLogging(battle, out var battleId) ||
                            !objects.TryGetIdWithLogging(MobileParty.MainParty, out var partyId)) return;
                        BattleStartCoordinator.Instance?.RequestBlocking(BattleStartMode.Mission, battleId, partyId);
                    }
                    finally
                    {
                        pendingBattles.Remove(quest);
                    }
                }));
        }
        catch
        {
            pendingBattles.Remove(quest);
            throw;
        }
    }

    private bool TryGetQuest(string giverId, string issueId, out Quest quest)
    {
        quest = null;
        if (!issues.TryResolve(giverId, issueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest current) || !current.IsOngoing) return false;
        quest = current;
        return true;
    }
}
