using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapEvents;

internal enum BattlePartyClaimStatus
{
    NoGrant,
    Accepted,
    Rejected
}

internal sealed class BattlePartyClaim
{
    internal readonly string ControllerId;
    internal readonly long Generation;
    internal readonly TroopRosterData MemberSourceDelta;
    internal readonly TroopRosterData PrisonerSourceDelta;
    internal long Token;

    internal BattlePartyClaim(
        string controllerId,
        long generation,
        TroopRosterData memberSourceDelta,
        TroopRosterData prisonerSourceDelta)
    {
        ControllerId = controllerId;
        Generation = generation;
        MemberSourceDelta = memberSourceDelta;
        PrisonerSourceDelta = prisonerSourceDelta;
    }
}

internal interface IBattlePartyGrantRegistry
{
    void Stage(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        string mapEventId,
        TroopRosterData awardedMembers,
        TroopRosterData awardedPrisoners);

    void Forfeit(string controllerId);

    BattlePartyClaimStatus TryPrepareClaim(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        TroopRosterData memberSourceDelta,
        TroopRosterData prisonerSourceDelta,
        out BattlePartyClaim claim,
        out string reason);

    bool TryActivate(BattlePartyClaim claim);
    bool Consume(BattlePartyClaim claim);
    void Release(BattlePartyClaim claim);
}

/// <summary>
/// Session-scoped authority for the ownerless temporary member/prisoner rosters shown after a
/// battle. The client sends only their deltas when it closes that party screen, so the normal
/// PartyBase ownership checks cannot authenticate the source. This registry binds those deltas
/// to the exact server-authored award and permits one reversible commit.
/// </summary>
internal sealed class BattlePartyGrantRegistry : IBattlePartyGrantRegistry
{
    private const int TombstoneLimitPerController = 64;
    private static readonly ILogger Logger =
        LogManager.GetLogger<BattlePartyGrantRegistry>();

    private readonly struct Award
    {
        internal readonly int Number;
        internal readonly int Wounded;
        internal readonly int Xp;

        internal Award(int number, int wounded, int xp)
        {
            Number = number;
            Wounded = wounded;
            Xp = xp;
        }
    }

    private sealed class Grant
    {
        internal readonly string OwnerHeroId;
        internal readonly string OwnerPartyId;
        internal readonly string MapEventId;
        internal readonly long Generation;
        internal readonly Dictionary<string, Award> Members;
        internal readonly Dictionary<string, Award> Prisoners;
        internal long ActiveToken;

        internal Grant(
            string ownerHeroId,
            string ownerPartyId,
            string mapEventId,
            long generation,
            Dictionary<string, Award> members,
            Dictionary<string, Award> prisoners)
        {
            OwnerHeroId = ownerHeroId;
            OwnerPartyId = ownerPartyId;
            MapEventId = mapEventId;
            Generation = generation;
            Members = members;
            Prisoners = prisoners;
        }
    }

    private sealed class Tombstones
    {
        internal readonly Queue<string> Order = new();
        internal readonly HashSet<string> MapEventIds =
            new(StringComparer.Ordinal);
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Grant> grants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Tombstones> tombstones =
        new(StringComparer.Ordinal);
    private readonly IObjectManager objectManager;
    private long nextGeneration;
    private long nextToken;

    public BattlePartyGrantRegistry(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Stage(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        string mapEventId,
        TroopRosterData awardedMembers,
        TroopRosterData awardedPrisoners)
    {
        if (string.IsNullOrEmpty(controllerId) ||
            string.IsNullOrEmpty(ownerHeroId) ||
            string.IsNullOrEmpty(ownerPartyId) ||
            string.IsNullOrEmpty(mapEventId))
            return;

        lock (gate)
        {
            if (IsSettledLocked(controllerId, mapEventId))
                return;
            if (grants.TryGetValue(controllerId, out Grant existing) &&
                string.Equals(
                    existing.MapEventId, mapEventId, StringComparison.Ordinal))
                return;

            Dictionary<string, Award> members =
                ReadAward(awardedMembers, out bool valid);
            Dictionary<string, Award> prisoners =
                ReadAward(awardedPrisoners, out bool prisonersValid);
            if (!valid || !prisonersValid ||
                members.Count == 0 && prisoners.Count == 0)
            {
                ForfeitLocked(controllerId);
                RememberSettledLocked(controllerId, mapEventId);
                return;
            }

            if (existing != null)
            {
                Logger.Warning(
                    "Forfeiting unclaimed battle party award for controller {ControllerId}, " +
                    "map event {OldMapEventId}, because {NewMapEventId} arrived",
                    controllerId,
                    existing.MapEventId,
                    mapEventId);
                ForfeitLocked(controllerId);
            }

            grants[controllerId] = new Grant(
                ownerHeroId,
                ownerPartyId,
                mapEventId,
                ++nextGeneration,
                members,
                prisoners);
        }
    }

    public void Forfeit(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId))
            return;
        lock (gate)
            ForfeitLocked(controllerId);
    }

    public BattlePartyClaimStatus TryPrepareClaim(
        string controllerId,
        string ownerHeroId,
        string ownerPartyId,
        TroopRosterData memberSourceDelta,
        TroopRosterData prisonerSourceDelta,
        out BattlePartyClaim claim,
        out string reason)
    {
        claim = null;
        reason = null;
        lock (gate)
        {
            if (string.IsNullOrEmpty(controllerId) ||
                !grants.TryGetValue(controllerId, out Grant grant) ||
                !string.Equals(
                    grant.OwnerHeroId, ownerHeroId, StringComparison.Ordinal) ||
                !string.Equals(
                    grant.OwnerPartyId, ownerPartyId, StringComparison.Ordinal))
                return BattlePartyClaimStatus.NoGrant;
            if (grant.ActiveToken != 0)
            {
                reason = "Your previous post-battle party claim is still being processed.";
                return BattlePartyClaimStatus.Rejected;
            }
            if (!ValidateRemovalDelta(
                    grant.Members, memberSourceDelta, out reason) ||
                !ValidateRemovalDelta(
                    grant.Prisoners, prisonerSourceDelta, out reason))
                return BattlePartyClaimStatus.Rejected;

            claim = new BattlePartyClaim(
                controllerId,
                grant.Generation,
                NormalizeDelta(memberSourceDelta),
                NormalizeDelta(prisonerSourceDelta));
            return BattlePartyClaimStatus.Accepted;
        }
    }

    public bool TryActivate(BattlePartyClaim claim)
    {
        if (claim == null)
            return true;
        lock (gate)
        {
            if (!grants.TryGetValue(claim.ControllerId, out Grant grant) ||
                grant.Generation != claim.Generation ||
                grant.ActiveToken != 0)
                return false;
            claim.Token = ++nextToken;
            grant.ActiveToken = claim.Token;
            return true;
        }
    }

    public bool Consume(BattlePartyClaim claim)
    {
        if (claim == null)
            return true;
        lock (gate)
        {
            if (!TryGetMatchingGrant(claim, out Grant grant))
                return false;
            RememberSettledLocked(claim.ControllerId, grant.MapEventId);
            grants.Remove(claim.ControllerId);
            return true;
        }
    }

    public void Release(BattlePartyClaim claim)
    {
        if (claim == null || claim.Token == 0)
            return;
        lock (gate)
        {
            if (TryGetMatchingGrant(claim, out Grant grant))
                grant.ActiveToken = 0;
            claim.Token = 0;
        }
    }

    private bool TryGetMatchingGrant(BattlePartyClaim claim, out Grant grant) =>
        grants.TryGetValue(claim.ControllerId, out grant) &&
        grant.Generation == claim.Generation &&
        grant.ActiveToken == claim.Token && claim.Token != 0;

    private Dictionary<string, Award> ReadAward(
        TroopRosterData data,
        out bool valid)
    {
        valid = true;
        var result = new Dictionary<string, Award>(StringComparer.Ordinal);
        foreach (TroopRosterElementData element in
                 data.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (string.IsNullOrEmpty(element.CharacterId) ||
                element.Number <= 0 ||
                element.WoundedNumber < 0 ||
                element.WoundedNumber > element.Number ||
                element.Xp < 0 ||
                result.ContainsKey(element.CharacterId))
            {
                valid = false;
                return result;
            }
            if (!objectManager.TryGetObject(
                    element.CharacterId, out CharacterObject character) ||
                character == null)
            {
                valid = false;
                return result;
            }
            // Remote player heroes use the dedicated captivity flow. Native battle results do,
            // however, award captured AI lords in this temporary roster; retaining those exact
            // NPC hero IDs lets PartyDone validate the removal before TakePrisonerAction commits it.
            if (character.IsHero &&
                character.HeroObject?.IsPlayerHero() == true)
                continue;
            result.Add(
                element.CharacterId,
                new Award(
                    element.Number, element.WoundedNumber, element.Xp));
        }
        return result;
    }

    private bool ValidateRemovalDelta(
        IReadOnlyDictionary<string, Award> award,
        TroopRosterData delta,
        out string reason)
    {
        reason = "The selected post-battle troops no longer match the server award.";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TroopRosterElementData element in
                 delta.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (string.IsNullOrEmpty(element.CharacterId) ||
                !seen.Add(element.CharacterId))
                return false;
            if (!objectManager.TryGetObject(
                    element.CharacterId, out CharacterObject character) ||
                character == null ||
                character.IsHero &&
                    character.HeroObject?.IsPlayerHero() == true)
                return false;
            if (element.Number == 0 &&
                element.WoundedNumber == 0 && element.Xp == 0)
                continue;
            award.TryGetValue(element.CharacterId, out Award available);
            // PartyDone transmits one net delta per character. A player can take
            // awarded wounded troops while leaving healthy troops of the same
            // character behind (or the reverse), so the individual number,
            // wounded and XP components are not required to share a sign. The
            // authoritative invariant is that applying the net delta to the
            // immutable virtual award leaves a valid roster element. Global role
            // conservation and the authoritative right-roster commit separately
            // prove that any positive virtual-left contents came from the player.
            long finalNumber = available.Number + (long)element.Number;
            long finalWounded = available.Wounded +
                (long)element.WoundedNumber;
            long finalXp = available.Xp + (long)element.Xp;
            if (finalNumber < 0 || finalNumber > int.MaxValue ||
                finalWounded < 0 || finalWounded > finalNumber ||
                finalXp < 0 || finalXp > int.MaxValue)
                return false;
        }
        return true;
    }

    private static TroopRosterData NormalizeDelta(TroopRosterData data) =>
        new((data.Data ?? Array.Empty<TroopRosterElementData>()).Where(element =>
            element.Number != 0 || element.WoundedNumber != 0 ||
            element.Xp != 0));

    private void ForfeitLocked(string controllerId)
    {
        if (!grants.TryGetValue(controllerId, out Grant grant))
            return;
        RememberSettledLocked(controllerId, grant.MapEventId);
        grants.Remove(controllerId);
    }

    private bool IsSettledLocked(string controllerId, string mapEventId) =>
        tombstones.TryGetValue(controllerId, out Tombstones settled) &&
        settled.MapEventIds.Contains(mapEventId);

    private void RememberSettledLocked(string controllerId, string mapEventId)
    {
        if (string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(mapEventId))
            return;
        if (!tombstones.TryGetValue(controllerId, out Tombstones settled))
        {
            settled = new Tombstones();
            tombstones.Add(controllerId, settled);
        }
        if (!settled.MapEventIds.Add(mapEventId))
            return;
        settled.Order.Enqueue(mapEventId);
        while (settled.Order.Count > TombstoneLimitPerController)
            settled.MapEventIds.Remove(settled.Order.Dequeue());
    }
}
