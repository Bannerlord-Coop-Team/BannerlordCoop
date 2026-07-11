using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed partial class TournamentSessionHandler
{
    private void Handle_HitProgression(MessagePayload<NetworkSubmitTournamentHitProgression> payload)
    {
        if (ModInformation.IsClient || !TryAuthenticate(payload.Who, out var peer, out var player))
            return;

        GameThread.RunSafe(() =>
        {
            TournamentHitProgressionData data = payload.What.Data;
            TournamentSessionSnapshot snapshot = null;
            if (!IsValidProgressionData(data) ||
                !sessionRegistry.TryGet(data.SessionId, out snapshot) ||
                snapshot.Phase != TournamentSessionPhase.LiveMatch ||
                snapshot.HostControllerId != player.ControllerId ||
                snapshot.CurrentMatchId != data.MatchId ||
                snapshot.Revision != data.Revision ||
                snapshot.BracketRevision != data.BracketRevision ||
                !sessionRegistry.TryGetSpawnManifest(data.SessionId, out var manifest) ||
                manifest.MatchId != data.MatchId ||
                manifest.BracketRevision != data.BracketRevision ||
                !TryResolveManifestCharacter(manifest, data.AttackerAgentId, out var attackerData, out var attacker, true) ||
                !TryResolveManifestCharacter(manifest, data.VictimAgentId, out _, out var victim))
            {
                SendCanonical(peer, snapshot);
                return;
            }

            TournamentContestantData contestant = snapshot.Contestants.FirstOrDefault(candidate =>
                candidate.SlotId == attackerData.SlotId);
            if (contestant == null ||
                !contestant.IsHuman ||
                contestant.IsReplaced ||
                contestant.ControllerId != data.DamageOriginControllerId ||
                !TryResolveWeapon(data, out var weapon))
            {
                SendCanonical(peer, snapshot);
                return;
            }

            string dedupeKey = $"{data.SessionId}\n{data.MatchId}\n{data.DamageOriginControllerId}\n{data.DamageSequence}";
            if (acceptedHitProgression.Contains(dedupeKey))
                return;

            SkillLevelingManager.OnCombatHit(
                attacker,
                victim,
                null,
                null,
                data.MovementSpeedModifier,
                data.ShotDifficulty,
                weapon,
                data.HitpointRatio,
                CombatXpModel.MissionTypeEnum.Tournament,
                data.AttackerMounted,
                data.SameTeam,
                false,
                data.DamageAmount,
                data.Fatal,
                false,
                data.Charging,
                data.SneakAttack);
            acceptedHitProgression.Add(dedupeKey);
            liveCombatSessions.Add(data.SessionId);
        }, context: nameof(Handle_HitProgression));
    }

    private bool TryResolveManifestCharacter(
        TournamentSpawnManifestData manifest,
        Guid agentId,
        out TournamentAgentSpawnData agentData,
        out CharacterObject character,
        bool resolveRider = false)
    {
        agentData = manifest.Agents.FirstOrDefault(candidate =>
            candidate.AgentId == agentId || candidate.MountAgentId == agentId);
        character = null;
        if (agentData == null)
            return false;

        string characterId = resolveRider || agentData.AgentId == agentId
            ? agentData.CharacterId
            : agentData.MountCharacterId;
        return !string.IsNullOrEmpty(characterId) &&
            objectManager.TryGetObject(characterId, out character);
    }

    private bool TryResolveWeapon(
        TournamentHitProgressionData data,
        out WeaponComponentData weapon)
    {
        weapon = null;
        if (string.IsNullOrEmpty(data.WeaponItemId))
            return data.WeaponUsageIndex == -1;
        if (!objectManager.TryGetObject(data.WeaponItemId, out ItemObject item) ||
            data.WeaponUsageIndex < 0 ||
            data.WeaponUsageIndex >= item.Weapons.Count)
        {
            return false;
        }

        weapon = item.Weapons[data.WeaponUsageIndex];
        return weapon != null;
    }

    private static bool IsValidProgressionData(TournamentHitProgressionData data)
    {
        return data != null &&
            !string.IsNullOrEmpty(data.SessionId) && data.SessionId.Length <= 256 &&
            !string.IsNullOrEmpty(data.MatchId) && data.MatchId.Length <= 256 &&
            !string.IsNullOrEmpty(data.DamageOriginControllerId) && data.DamageOriginControllerId.Length <= 256 &&
            data.DamageSequence > 0 &&
            data.AttackerAgentId != Guid.Empty &&
            data.VictimAgentId != Guid.Empty &&
            (data.WeaponItemId?.Length ?? 0) <= 256 &&
            data.AttackType >= 0 && data.AttackType <= 16 &&
            IsFinite(data.MovementSpeedModifier) &&
            IsFinite(data.ShotDifficulty) && data.ShotDifficulty >= 0f &&
            IsFinite(data.HitpointRatio) && data.HitpointRatio >= 0f && data.HitpointRatio <= 1f &&
            IsFinite(data.DamageAmount) && data.DamageAmount >= 0f;
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}
