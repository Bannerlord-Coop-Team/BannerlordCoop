using Common;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

internal interface IRevenueFarmingActions
{
    void Request(Quest quest, RevenueFarmingAction action, string incident = null, int option = 0);
    bool Execute(RevenueFarmingSession session, RequestRevenueFarmingAction request);
}

internal sealed class RevenueFarmingActions : IRevenueFarmingActions
{
    private readonly IRevenueFarmingSessionStore sessions;
    private readonly IObjectManager objects;
    private readonly INetwork network;

    public RevenueFarmingActions(IRevenueFarmingSessionStore sessions, IObjectManager objects, INetwork network)
    {
        this.sessions = sessions;
        this.objects = objects;
        this.network = network;
    }

    public void Request(Quest quest, RevenueFarmingAction action, string incident = null, int option = 0)
    {
        if (ModInformation.IsServer || quest == null || !quest.IsOngoing) return;
        var session = sessions.Get(quest);
        if (session == null || !sessions.IsLocal(quest) || !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;
        if (action == RevenueFarmingAction.PayGiver || action == RevenueFarmingAction.BetrayGiver ||
            action == RevenueFarmingAction.PayDemand || action == RevenueFarmingAction.BetrayDemand ||
            action == RevenueFarmingAction.PaySteward)
        {
            if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
                dispatcher.BeforeQuestBranch(quest, (byte)action);
            return;
        }
        network.SendAll(new RequestRevenueFarmingAction
        {
            GiverId = giverId, QuestId = quest.StringId, Revision = session.Revision,
            Action = action, Incident = incident, Option = option,
        });
    }

    public bool Execute(RevenueFarmingSession session, RequestRevenueFarmingAction request)
    {
        if (ModInformation.IsClient || !session.Quest.IsOngoing || session.Revision != request.Revision) return false;
        var hero = session.PlayerHero;
        if (!hero.IsAlive) return false;
        if (request.Action != RevenueFarmingAction.PayDemand && request.Action != RevenueFarmingAction.BetrayDemand &&
            (hero.IsPrisoner || hero.PartyBelongedTo == null || hero.PartyBelongedTo != session.PlayerParty)) return false;
        using (new RevenueFarmingExecutionScope(session))
        {
            var behavior = Campaign.Current.GetCampaignBehavior<RevenueFarmingIssueBehavior>();
            var args = new MenuCallbackArgs((MenuContext)null, null);
            switch (request.Action)
            {
                case RevenueFarmingAction.Collect:
                    if (!CanCollect(session, behavior, args)) return false;
                    session.CollectionSettlement = Settlement.CurrentSettlement;
                    if (session.Incident != null)
                        GameMenu.SwitchToMenu(session.Incident);
                    else
                        behavior.collect_revenue_menu_consequence(args);
                    return true;
                case RevenueFarmingAction.Leave:
                    if (session.CollectionSettlement == null || session.CollectionSettlement != Settlement.CurrentSettlement) return false;
                    behavior.leave_consequence(args);
                    return true;
                case RevenueFarmingAction.Desert:
                    session.Quest.CollectingRevenues = false;
                    return true;
                case RevenueFarmingAction.ResolveIncident:
                    if (session.AwaitingPayment || session.Incident == null || request.Incident != session.Incident ||
                        session.CollectionSettlement != Settlement.CurrentSettlement || hero.PartyBelongedTo.MapEvent != null ||
                        session.Quest.FindCurrentRevenueVillage().GetIsCompleted()) return false;
                    var incident = behavior._villageEvents.FirstOrDefault(item => item.Id == session.Incident);
                    if (incident == null || request.Option < 0 || request.Option >= incident.OptionConditionsAndConsequences.Count) return false;
                    var option = incident.OptionConditionsAndConsequences[request.Option];
                    if (!option.OnCondition(args) || !args.IsEnabled) return false;
                    session.Incident = null;
                    option.OnConsequence(args);
                    return true;
                case RevenueFarmingAction.PaySteward:
                    if (Settlement.CurrentSettlement == null ||
                        !(Settlement.CurrentSettlement.IsTown || Settlement.CurrentSettlement.IsCastle) ||
                        !behavior.talk_to_steward_on_condition(args) || !args.IsEnabled) return false;
                    behavior.talk_to_steward_on_consequence(args);
                    return true;
                case RevenueFarmingAction.PayGiver:
                case RevenueFarmingAction.BetrayGiver:
                    if (!session.Quest._allRevenuesAreCollected) return false;
                    return Complete(session, request.Action == RevenueFarmingAction.PayGiver);
                case RevenueFarmingAction.PayDemand:
                case RevenueFarmingAction.BetrayDemand:
                    if (!session.AwaitingPayment || !session.Quest.QuestDueTime.IsPast) return false;
                    return Complete(session, request.Action == RevenueFarmingAction.PayDemand);
                default:
                    return false;
            }
        }
    }

    private static bool CanCollect(RevenueFarmingSession session, RevenueFarmingIssueBehavior behavior, MenuCallbackArgs args)
    {
        var party = session.PlayerHero.PartyBelongedTo;
        if (session.AwaitingPayment || party.CurrentSettlement?.Village == null || party.MapEvent != null ||
            (session.Incident != null && session.CollectionSettlement != party.CurrentSettlement)) return false;
        return behavior.collect_revenue_menu_condition(args) && args.IsEnabled;
    }

    private static bool Complete(RevenueFarmingSession session, bool pay)
    {
        if (pay && session.PlayerHero.Gold < session.Quest._totalRequestedDenars) return false;
        if (pay) session.Quest.QuestCompletedWithSuccess();
        else session.Quest.QuestCompletedWithBetray();
        return true;
    }
}
