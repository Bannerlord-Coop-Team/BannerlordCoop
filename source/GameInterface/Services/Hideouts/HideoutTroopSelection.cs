using GameInterface.Configuration;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Hideouts;

[ProtoContract(SkipConstructor = true)]
public readonly struct HideoutTroopSelectionEntry
{
    [ProtoMember(1)] public string CharacterId { get; }
    [ProtoMember(2)] public int Count { get; }

    public HideoutTroopSelectionEntry(string characterId, int count)
    {
        CharacterId = characterId;
        Count = count;
    }
}

public interface IHideoutTroopSelection : IGameAbstraction
{
    bool TrySelect(Settlement settlement, MobileParty party, string controllerId, int escortLimit,
        IReadOnlyList<HideoutTroopSelectionEntry> requested,
        out HideoutTroopSelectionEntry[] accepted, out int remaining);
    int GetRemaining(Settlement settlement, int defaultLimit);
    bool HasSelection(Settlement settlement, MobileParty party);
    void CancelUnbound(Settlement settlement, MobileParty party);
    void BindMapEvent(Settlement settlement, MapEvent mapEvent);
    void FilterReserve(MapEvent mapEvent, MapEventParty party, List<TroopReserveEntry> entries);
    void ForgetMapEvent(MapEvent mapEvent);
}

/// <summary>One escort budget per hideout attempt, fixed by the first accepted player's native allowance.</summary>
public sealed class HideoutTroopSelection : IHideoutTroopSelection
{
    private sealed class Attempt
    {
        public int Limit;
        public int Used;
        public MapEvent MapEvent;
        public readonly Dictionary<string, HideoutTroopSelectionEntry[]> Parties = new();
    }

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly Dictionary<Settlement, Attempt> attempts = new();
    private readonly object gate = new();

    public HideoutTroopSelection(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public bool TrySelect(Settlement settlement, MobileParty party, string controllerId, int escortLimit,
        IReadOnlyList<HideoutTroopSelectionEntry> requested,
        out HideoutTroopSelectionEntry[] accepted, out int remaining)
    {
        accepted = Array.Empty<HideoutTroopSelectionEntry>();
        remaining = GetRemaining(settlement, escortLimit);
        if (settlement?.IsHideout != true || party == null || requested == null || escortLimit < 0 ||
            !objectManager.TryGetId(party, out var partyId) ||
            !playerManager.TryGetPlayer(controllerId, out var player) || player.MobilePartyId != partyId ||
            string.IsNullOrEmpty(player.CharacterObjectId))
            return false;

        lock (gate)
        {
            attempts.TryGetValue(settlement, out var attempt);
            if (attempt != null && attempt.Parties.TryGetValue(partyId, out var existing))
            {
                accepted = existing.ToArray();
                remaining = attempt.Limit - attempt.Used;
                return true;
            }

            var healthy = new Dictionary<string, int>();
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || !objectManager.TryGetId(element.Character, out var characterId))
                    continue;

                var count = element.Number - element.WoundedNumber;
                if (characterId == player.CharacterObjectId && ModConfigProvider.ModOptions.PlayerWoundedBattleEntry)
                    count = element.Number;
                healthy[characterId] = count;
            }

            if (!healthy.TryGetValue(player.CharacterObjectId, out var heroes) || heroes < 1)
                return false;

            var selection = new List<HideoutTroopSelectionEntry>
            {
                new HideoutTroopSelectionEntry(player.CharacterObjectId, 1),
            };
            var selectedCounts = new Dictionary<string, int>();
            var escorts = 0;
            remaining = attempt == null ? escortLimit : attempt.Limit - attempt.Used;
            foreach (var entry in requested)
            {
                if (string.IsNullOrEmpty(entry.CharacterId) || entry.Count < 1)
                    return false;
                if (entry.CharacterId == player.CharacterObjectId)
                {
                    if (entry.Count != 1) return false;
                    continue;
                }

                selectedCounts.TryGetValue(entry.CharacterId, out var alreadySelected);
                if (!healthy.TryGetValue(entry.CharacterId, out var available) ||
                    entry.Count > available - alreadySelected || entry.Count > remaining - escorts)
                    return false;

                selectedCounts[entry.CharacterId] = alreadySelected + entry.Count;
                escorts += entry.Count;
                selection.Add(entry);
            }

            if (attempt == null)
            {
                attempt = new Attempt { Limit = escortLimit };
                attempts.Add(settlement, attempt);
            }
            accepted = selection.ToArray();
            attempt.Parties.Add(partyId, accepted.ToArray());
            attempt.Used += escorts;
            remaining = attempt.Limit - attempt.Used;
            return true;
        }
    }

    public int GetRemaining(Settlement settlement, int defaultLimit)
    {
        lock (gate)
            return settlement != null && attempts.TryGetValue(settlement, out var attempt)
                ? attempt.Limit - attempt.Used
                : Math.Max(0, defaultLimit);
    }

    public bool HasSelection(Settlement settlement, MobileParty party)
    {
        if (settlement == null || party == null || !objectManager.TryGetId(party, out var partyId))
            return false;
        lock (gate)
            return attempts.TryGetValue(settlement, out var attempt) && attempt.Parties.ContainsKey(partyId);
    }

    public void CancelUnbound(Settlement settlement, MobileParty party)
    {
        if (settlement == null || party == null || !objectManager.TryGetId(party, out var partyId)) return;
        lock (gate)
        {
            if (!attempts.TryGetValue(settlement, out var attempt) || attempt.MapEvent != null ||
                !attempt.Parties.TryGetValue(partyId, out var selection))
                return;

            attempt.Used -= selection.Sum(entry => entry.Count) - 1;
            attempt.Parties.Remove(partyId);
            if (attempt.Parties.Count == 0) attempts.Remove(settlement);
        }
    }

    public void BindMapEvent(Settlement settlement, MapEvent mapEvent)
    {
        if (settlement == null || mapEvent == null) return;
        lock (gate)
            if (attempts.TryGetValue(settlement, out var attempt) && attempt.MapEvent == null)
                attempt.MapEvent = mapEvent;
    }

    public void FilterReserve(MapEvent mapEvent, MapEventParty party, List<TroopReserveEntry> entries)
    {
        if (!mapEvent.IsHideoutBattle || party.Party?.Side != BattleSideEnum.Attacker) return;
        lock (gate)
        {
            var settlement = mapEvent.MapEventSettlement;
            var mobileParty = party.Party.MobileParty;
            if (settlement == null || mobileParty == null ||
                !attempts.TryGetValue(settlement, out var attempt) || !ReferenceEquals(attempt.MapEvent, mapEvent) ||
                !objectManager.TryGetId(mobileParty, out var partyId) ||
                !attempt.Parties.TryGetValue(partyId, out var selection))
            {
                entries.Clear();
                return;
            }

            var selected = new List<TroopReserveEntry>();
            var available = entries.GroupBy(entry => entry.CharacterId)
                .ToDictionary(group => group.Key, group => new Queue<TroopReserveEntry>(group));
            foreach (var entry in selection)
            {
                if (!available.TryGetValue(entry.CharacterId, out var queue)) continue;
                for (var count = 0; count < entry.Count && queue.Count > 0; count++)
                    selected.Add(queue.Dequeue());
            }
            entries.Clear();
            entries.AddRange(selected);
        }
    }

    public void ForgetMapEvent(MapEvent mapEvent)
    {
        if (mapEvent == null) return;
        lock (gate)
            foreach (var settlement in attempts.Where(pair => ReferenceEquals(pair.Value.MapEvent, mapEvent))
                         .Select(pair => pair.Key).ToArray())
                attempts.Remove(settlement);
    }
}
