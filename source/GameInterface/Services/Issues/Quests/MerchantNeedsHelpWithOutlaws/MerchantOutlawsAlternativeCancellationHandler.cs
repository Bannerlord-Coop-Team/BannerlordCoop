using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

public readonly struct MerchantOutlawsAlternativeCancelled : IEvent
{
    public IssueBase Issue { get; }
    public bool Finalizing { get; }

    public MerchantOutlawsAlternativeCancelled(IssueBase issue, bool finalizing = false)
    {
        Issue = issue;
        Finalizing = finalizing;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestMerchantOutlawsAlternativeCancellation : ICommand
{
    [ProtoMember(3)] public TroopRosterElementData[] Troops { get; }

    public RequestMerchantOutlawsAlternativeCancellation(TroopRosterElementData[] troops)
    {
        Troops = troops;
    }
}

internal class MerchantOutlawsAlternativeCancellationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IAlternativeSolutionTroopValidator troops;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly HashSet<TroopRoster> committedSelections = new HashSet<TroopRoster>();
    private readonly HashSet<IssueBase> pendingAccepts = new HashSet<IssueBase>();
    private readonly HashSet<TroopRoster> acceptedSelections = new HashSet<TroopRoster>();

    public MerchantOutlawsAlternativeCancellationHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager,
        IAlternativeSolutionTroopValidator troops, IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.troops = troops;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<MerchantOutlawsAlternativeCancelled>(Handle_Local);
        messageBroker.Subscribe<PartyDoneLogicAttempted>(Handle_Done);
        messageBroker.Subscribe<AlternativeSolutionAcceptRequested>(Handle_AcceptRequested);
        messageBroker.Subscribe<NetworkAlternativeSolutionAccepted>(Handle_Accepted);
        messageBroker.Subscribe<RequestMerchantOutlawsAlternativeCancellation>(Handle_Request);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MerchantOutlawsAlternativeCancelled>(Handle_Local);
        messageBroker.Unsubscribe<PartyDoneLogicAttempted>(Handle_Done);
        messageBroker.Unsubscribe<AlternativeSolutionAcceptRequested>(Handle_AcceptRequested);
        messageBroker.Unsubscribe<NetworkAlternativeSolutionAccepted>(Handle_Accepted);
        messageBroker.Unsubscribe<RequestMerchantOutlawsAlternativeCancellation>(Handle_Request);
    }

    private void Handle_Local(MessagePayload<MerchantOutlawsAlternativeCancelled> payload)
    {
        if (ModInformation.IsServer) return;
        var issue = payload.What.Issue;
        if (payload.What.Finalizing && acceptedSelections.Remove(issue.AlternativeSolutionSentTroops)) return;
        pendingAccepts.Remove(issue);
        if (committedSelections.Remove(issue.AlternativeSolutionSentTroops))
        {
            network.SendAll(new RequestMerchantOutlawsAlternativeCancellation(
                troops.ToData(issue.AlternativeSolutionSentTroops)));
        }
        using (new AllowedThread()) troops.ReturnToMainParty(issue);
    }

    private void Handle_Done(MessagePayload<PartyDoneLogicAttempted> payload)
    {
        if (ModInformation.IsServer || payload.What.PartyScreenMode != Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage) return;
        var roster = payload.What.LeftMemberRoster;
        if (Campaign.Current.IssueManager.Issues.Values.Any(issue =>
            issue is MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue &&
            ReferenceEquals(issue.AlternativeSolutionSentTroops, roster))) committedSelections.Add(roster);
    }

    private void Handle_AcceptRequested(MessagePayload<AlternativeSolutionAcceptRequested> payload)
    {
        if (ModInformation.IsServer) return;
        // Acceptance now owns the refund through the framework's rejection path.
        committedSelections.Remove(payload.What.Issue.AlternativeSolutionSentTroops);
        pendingAccepts.Add(payload.What.Issue);
    }

    private void Handle_Accepted(MessagePayload<NetworkAlternativeSolutionAccepted> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            foreach (var issue in pendingAccepts.ToArray())
            {
                if (issue.StringId != data.IssueId ||
                    !objectManager.TryGetId(issue.IssueOwner, out var giverId) || giverId != data.IssueOwnerId) continue;
                if (controllerIdProvider.ControllerId != data.ControllerId) continue;
                pendingAccepts.Remove(issue);
                acceptedSelections.Add(issue.AlternativeSolutionSentTroops);
            }
        }, context: nameof(MerchantOutlawsAlternativeCancellationHandler));
    }

    private void Handle_Request(MessagePayload<RequestMerchantOutlawsAlternativeCancellation> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                hero.PartyBelongedTo == null) return;

            // The roster screen already committed troop removal; declining the follow-up must undo it on the server.
            troops.ReturnTroopsToParty(hero.PartyBelongedTo, data.Troops);
        }, context: nameof(MerchantOutlawsAlternativeCancellationHandler));
    }
}
