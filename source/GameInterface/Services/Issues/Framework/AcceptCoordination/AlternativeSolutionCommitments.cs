using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
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
    private readonly IPlayerManager players;
    private readonly INetwork network;
    private readonly Dictionary<(string Hero, string Giver, string Issue), (NetPeer Peer, Dictionary<CharacterObject, TroopRosterElement> Troops)> selections = new();

    public AlternativeSolutionCommitments(IMessageBroker broker, IObjectManager objects,
        ITroopRosterInterface rosters, IPlayerManager players, INetwork network)
    {
        this.broker = broker;
        this.objects = objects;
        this.rosters = rosters;
        this.players = players;
        this.network = network;
        broker.Subscribe<PlayerDisconnected>(Handle_Disconnected);
    }

    public void Dispose()
    {
        broker.Unsubscribe<PlayerDisconnected>(Handle_Disconnected);
    }

    public IReadOnlyCollection<TroopRosterElement> GetSelection(NetPeer requester, string issueOwnerId, string issueId)
    {
        if (requester == null || !players.TryGetPlayer(requester, out var player) ||
            !selections.TryGetValue((player.HeroId, issueOwnerId, issueId), out var selection))
            return System.Array.Empty<TroopRosterElement>();
        return selection.Troops.Values.ToArray();
    }

    public TroopRosterElementData[] Consume(string heroId, string issueOwnerId, string issueId, TroopRosterElementData[] requested)
    {
        var key = (heroId, issueOwnerId, issueId);
        var result = new Dictionary<CharacterObject, TroopRosterElement>();
        if (selections.TryGetValue(key, out var selection))
        {
            selections.Remove(key);
            foreach (var element in selection.Troops.Values) result.Add(element.Character, element);
        }
        foreach (var element in rosters.UnpackTroopRosterData(new TroopRosterData(requested ?? System.Array.Empty<TroopRosterElementData>())))
        {
            if (element.Character.IsHero) Add(result, element);
        }
        return ToData(result.Values);
    }

    public void ReturnForIssue(string issueOwnerId, string issueId)
    {
        foreach (var entry in selections.Where(entry => entry.Key.Giver == issueOwnerId && entry.Key.Issue == issueId).ToArray())
        {
            selections.Remove(entry.Key);
            if (!objects.TryGetObjectWithLogging<Hero>(entry.Key.Hero, out var hero) || hero.PartyBelongedTo == null) continue;
            foreach (var element in entry.Value.Troops.Values) hero.PartyBelongedTo.MemberRoster.Add(element);
        }
    }

    public void SetSelection(NetPeer requester, string issueOwnerId, string issueId, IEnumerable<TroopRosterElement> troops)
    {
        if (requester == null || !players.TryGetPlayer(requester, out var player)) return;

        // Companions stay with the client's picker until the accept.
        var selection = new Dictionary<CharacterObject, TroopRosterElement>();
        foreach (var element in troops)
        {
            if (!element.Character.IsHero) Add(selection, element);
        }
        selections[(player.HeroId, issueOwnerId, issueId)] = (requester, selection);
        network.Send(requester, new NetworkAlternativeSolutionSelection(issueOwnerId, issueId, ToData(selection.Values)));
    }

    private void Handle_Disconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            foreach (var entry in selections.Where(entry => entry.Value.Peer == payload.What.PlayerId).ToArray())
            {
                selections.Remove(entry.Key);
                if (!objects.TryGetObjectWithLogging<Hero>(entry.Key.Hero, out var hero) || hero.PartyBelongedTo == null) continue;
                foreach (var element in entry.Value.Troops.Values) hero.PartyBelongedTo.MemberRoster.Add(element);
            }
        }, context: nameof(AlternativeSolutionCommitments));
    }

    private static void Add(Dictionary<CharacterObject, TroopRosterElement> selection, TroopRosterElement delta)
    {
        if (!selection.TryGetValue(delta.Character, out var current) && delta.Number + delta.WoundedNumber <= 0) return;
        if (current.Character == null) current = new TroopRosterElement(delta.Character);
        var number = current.Number + delta.Number;
        if (number == 0)
        {
            selection.Remove(delta.Character);
            return;
        }
        selection[delta.Character] = new TroopRosterElement(delta.Character)
        {
            Number = number,
            WoundedNumber = System.Math.Min(number, current.WoundedNumber + delta.WoundedNumber),
            Xp = current.Xp + delta.Xp,
        };
    }

    private TroopRosterElementData[] ToData(IEnumerable<TroopRosterElement> selection)
    {
        var result = new List<TroopRosterElementData>();
        foreach (var element in selection)
        {
            if (objects.TryGetHandleWithLogging(element.Character, out var handle))
                result.Add(new TroopRosterElementData(handle, element.Number, element.WoundedNumber, element.Xp));
        }
        return result.ToArray();
    }
}
