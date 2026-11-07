using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>Attributes quest picker commits and restores locally staged companions when a selection closes.</summary>
internal class AlternativeSolutionPicker : IAlternativeSolutionPicker, IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IControllerIdProvider controller;
    private readonly IAlternativeSolutionCommitments commitments;
    private readonly IAlternativeSolutionTroopValidator troops;
    private readonly Dictionary<TroopRoster, (IssueBase Issue, PartyScreenLogic Logic)> pickers = new();

    public AlternativeSolutionPicker(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IControllerIdProvider controller, IAlternativeSolutionCommitments commitments,
        IAlternativeSolutionTroopValidator troops)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.controller = controller;
        this.commitments = commitments;
        this.troops = troops;
        broker.Subscribe<PartyDoneLogicAttempted>(Handle_Done);
        broker.Subscribe<RequestAlternativeSolutionCancellation>(Handle_Cancel);
        broker.Subscribe<NetworkAlternativeSolutionAccepted>(Handle_Accepted);
    }

    public void Dispose()
    {
        broker.Unsubscribe<PartyDoneLogicAttempted>(Handle_Done);
        broker.Unsubscribe<RequestAlternativeSolutionCancellation>(Handle_Cancel);
        broker.Unsubscribe<NetworkAlternativeSolutionAccepted>(Handle_Accepted);
        pickers.Clear();
    }

    public void Open(IssueBase issue)
    {
        if (ModInformation.IsClient && issue != null)
            pickers[issue.AlternativeSolutionSentTroops] = (issue, null);
    }

    public void Track(PartyScreenLogic logic)
    {
        if (ModInformation.IsClient && pickers.TryGetValue(logic.MemberRosters[0], out var picker))
            pickers[logic.MemberRosters[0]] = (picker.Issue, logic);
    }

    public bool TryGetIssue(TroopRoster roster, out string giverId, out string issueId)
    {
        giverId = null;
        issueId = null;
        if (roster == null || !pickers.TryGetValue(roster, out var picker) ||
            !objects.TryGetIdWithLogging(picker.Issue.IssueOwner, out giverId)) return false;
        issueId = picker.Issue.StringId;
        return true;
    }

    public void Close(IssueBase issue, bool cancel, bool closeScreen = true)
    {
        if (ModInformation.IsServer || !pickers.TryGetValue(issue.AlternativeSolutionSentTroops, out var picker)) return;
        pickers.Remove(issue.AlternativeSolutionSentTroops);
        using (new AllowedThread())
        {
            if (picker.Logic != null)
            {
                picker.Logic.Reset(true);
                if (closeScreen && Game.Current.GameStateManager.ActiveState is PartyState state && state.PartyScreenLogic == picker.Logic)
                    Game.Current.GameStateManager.PopState();
            }
            troops.ReturnToMainParty(issue);
        }
        if (cancel && objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId))
            network.SendAll(new RequestAlternativeSolutionCancellation(giverId, issue.StringId));
    }

    private void Handle_Done(MessagePayload<PartyDoneLogicAttempted> payload)
    {
        if (ModInformation.IsClient && payload.What.LeftMemberRoster != null &&
            pickers.TryGetValue(payload.What.LeftMemberRoster, out var picker))
            pickers[payload.What.LeftMemberRoster] = (picker.Issue, null);
    }

    private void Handle_Accepted(MessagePayload<NetworkAlternativeSolutionAccepted> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            foreach (var entry in pickers.ToArray())
            {
                if (entry.Value.Issue.StringId == data.IssueId &&
                    objects.TryGetId(entry.Value.Issue.IssueOwner, out var giverId) && giverId == data.IssueOwnerId)
                    pickers.Remove(entry.Key);
            }
        }, context: nameof(AlternativeSolutionPicker));
    }

    private void Handle_Cancel(MessagePayload<RequestAlternativeSolutionCancellation> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (!(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player) ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) || hero.PartyBelongedTo == null) return;
            var selected = commitments.Consume(player.HeroId, payload.What.IssueOwnerId, payload.What.IssueId, null);
            troops.ReturnTroopsToParty(hero.PartyBelongedTo, selected);
        }, context: nameof(AlternativeSolutionPicker));
    }
}
