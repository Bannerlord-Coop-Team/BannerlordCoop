using Common;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

internal class AlternativeSolutionCommitments : IHandler, IAlternativeSolutionCommitments
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly ITroopRosterInterface rosters;
    private readonly IAlternativeSolutionTroopValidator troops;
    private readonly Dictionary<(string Hero, string Giver, string Issue), (NetPeer Peer, TroopRoster Troops)> selections = new();

    public AlternativeSolutionCommitments(IMessageBroker broker, IObjectManager objects,
        ITroopRosterInterface rosters, IAlternativeSolutionTroopValidator troops)
    {
        this.broker = broker;
        this.objects = objects;
        this.rosters = rosters;
        this.troops = troops;
        broker.Subscribe<PartyDoneLogicApplied>(Handle_Applied);
        broker.Subscribe<PlayerDisconnected>(Handle_Disconnected);
    }

    public void Dispose()
    {
        broker.Unsubscribe<PartyDoneLogicApplied>(Handle_Applied);
        broker.Unsubscribe<PlayerDisconnected>(Handle_Disconnected);
    }

    public bool CanApply(string heroId, string issueOwnerId, string issueId, TroopRosterData delta)
    {
        selections.TryGetValue((heroId, issueOwnerId, issueId), out var selection);
        foreach (var element in rosters.UnpackTroopRosterData(delta))
        {
            if (element.Character.IsHero) continue;
            var available = selection.Troops == null ? default : selection.Troops.GetTroopRoster()
                .FirstOrDefault(value => value.Character == element.Character);
            if (available.Character == null) available = new TroopRosterElement(element.Character);
            if (-element.Number > available.Number || -element.WoundedNumber > available.WoundedNumber ||
                -element.Xp > available.Xp) return false;
        }
        return true;
    }

    public TroopRosterElementData[] Consume(string heroId, string issueOwnerId, string issueId, TroopRosterElementData[] requested)
    {
        var key = (heroId, issueOwnerId, issueId);
        var result = TroopRoster.CreateDummyTroopRoster();
        if (selections.TryGetValue(key, out var selection))
        {
            selections.Remove(key);
            result.Add(selection.Troops);
        }
        foreach (var element in rosters.UnpackTroopRosterData(new TroopRosterData(requested ?? System.Array.Empty<TroopRosterElementData>())))
        {
            if (element.Character.IsHero) result.Add(element);
        }
        return troops.ToData(result);
    }

    private void Handle_Applied(MessagePayload<PartyDoneLogicApplied> payload)
    {
        var data = payload.What;
        if (ModInformation.IsClient || data.Requester == null || string.IsNullOrEmpty(data.IssueId)) return;
        var key = (data.MainHeroId, data.IssueOwnerId, data.IssueId);
        if (!selections.TryGetValue(key, out var selection))
            selection = (data.Requester, TroopRoster.CreateDummyTroopRoster());
        foreach (var element in rosters.UnpackTroopRosterData(data.MemberDelta))
        {
            if (element.Character.IsHero) continue;
            selection.Troops.AddToCounts(element.Character, element.Number,
                woundedCount: element.WoundedNumber, xpChange: element.Xp);
        }
        selections[key] = selection;
    }

    private void Handle_Disconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            foreach (var entry in selections.Where(entry => entry.Value.Peer == payload.What.PlayerId).ToArray())
            {
                if (!objects.TryGetObjectWithLogging<Hero>(entry.Key.Hero, out var hero) || hero.PartyBelongedTo == null) continue;
                selections.Remove(entry.Key);
                troops.ReturnTroopsToParty(hero.PartyBelongedTo, troops.ToData(entry.Value.Troops));
            }
        }, context: nameof(AlternativeSolutionCommitments));
    }
}
