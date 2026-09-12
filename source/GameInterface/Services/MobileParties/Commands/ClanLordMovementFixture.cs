#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>Stages and observes the clan-lord conversation regression without starting a live run.</summary>
internal interface IClanLordMovementFixture
{
    CoopCommandResult Setup(string playerId);
    CoopCommandResult Observe(IReadOnlyList<string> args);
    CoopCommandResult Restore();
}

/// <summary>Owns one session-scoped fixture and retains captured state until restoration succeeds.</summary>
internal sealed class ClanLordMovementFixture : IClanLordMovementFixture
{
    private readonly IObjectManager objects;
    private readonly IMobilePartyBehaviorSnapshot snapshots;
    private readonly IMessageBroker messages;
    private readonly IClanLordMovementFixtureRules rules;
    private Campaign campaign;
    private Capture lordCapture;
    private Capture caravanCapture;
    private Observation observation;

    public ClanLordMovementFixture(IObjectManager objects, IMobilePartyBehaviorSnapshot snapshots,
        IMessageBroker messages, IClanLordMovementFixtureRules rules)
    {
        this.objects = objects;
        this.snapshots = snapshots;
        this.messages = messages;
        this.rules = rules;
    }

    public CoopCommandResult Setup(string playerId)
    {
        if (ModInformation.IsClient) return Result(false, "setup requires the server");
        if (!CheckCampaign()) return Result(false, "campaign is unavailable");
        if (lordCapture != null || caravanCapture != null)
            return Result(false, "fixture already captured; run clan_lord_fixture_restore first");
        if (!objects.TryGetObject(playerId, out MobileParty player) || !player.IsPlayerParty())
            return Result(false, "playerPartyId must be the exact registered player MobileParty id");
        string unavailable = Unavailable(player);
        if (unavailable != null) return Result(false, "player unavailable: " + unavailable);
        Clan clan = player.LeaderHero?.Clan;
        if (clan == null) return Result(false, "the player has no clan");
        if (Id(player.Party) == null || Id(player.LeaderHero) == null || Id(clan) == null || ConversationPartyTracker.Instance == null)
            return Result(false, "player, clan, or conversation tracking is not fully registered");

        var candidates = MobileParty.All.Where(p => p.IsLordParty && p.LeaderHero?.Clan == clan && p != player)
            .OrderBy(p => rules.LordPriority(p.LeaderHero.StringId))
            .ThenBy(p => p.LeaderHero.StringId, StringComparer.Ordinal)
            .ThenBy(p => p.StringId, StringComparer.Ordinal).ToArray();
        MobileParty lord = candidates.FirstOrDefault(p => EligibleAi(p) == null);
        var candidateEvidence = candidates.Select(p => new { party = Describe(p), rejected = EligibleAi(p) }).ToArray();
        if (lord == null) return Result(false, "no eligible registered lord in this player's clan", candidateEvidence);
        MobileParty caravan = MobileParty.All.Where(p => p.IsCaravan && EligibleAi(p) == null &&
                p.MapFaction != null && player.MapFaction != null && !p.MapFaction.IsAtWarWith(player.MapFaction))
            .OrderBy(p => p.Position.DistanceSquared(player.Position))
            .ThenBy(p => p.StringId, StringComparer.Ordinal).FirstOrDefault();
        if (caravan == null) return Result(false, "no eligible registered peaceful caravan", candidateEvidence);
        if (!TryPoint(player.Position, new[] { 0.3f, 0.6f, 0.9f }, out CampaignVec2 caravanPoint) ||
            !TryPoint(lord.Position, new[] { 24f, 20f, 16f }, out CampaignVec2 target))
            return Result(false, "no deterministic navigable staging point or lord route; nothing changed", candidateEvidence);
        if (!snapshots.TryCreate(lord, out var lordState) || !snapshots.CanApply(lord, lordState) ||
            !snapshots.TryCreate(caravan, out var caravanState) || !snapshots.CanApply(caravan, caravanState))
            return Result(false, "unable to capture restorable movement state; nothing changed", candidateEvidence);

        lordCapture = new Capture(lord, lordState);
        caravanCapture = new Capture(caravan, caravanState);
        observation = new Observation(Guid.NewGuid().ToString("N"), playerId, Id(lord), Id(caravan), target);
        try
        {
            caravanCapture.Modified = true;
            caravan.Position = caravanPoint;
            caravan.SetMoveModeHold();
            caravan.SetNavigationModeHold();
            caravan.Ai.SetDoNotMakeNewDecisions(true);
            if (!((IInteractablePoint)caravan.Party).CanPartyInteract(player, 0f))
                throw new InvalidOperationException("staged caravan is outside the real vanilla interaction range");
            lordCapture.Modified = true;
            lord.Ai.SetDoNotMakeNewDecisions(true);
            lord.SetMoveGoToPoint(target, MobileParty.NavigationType.Default);
            lord.SetShortTermBehavior(AiBehavior.GoToPoint, null);
            lord.Ai.BehaviorTarget = target;
            lord.SetNavigationModePoint(target);
            Publish(lord);
            Publish(caravan);
        }
        catch (Exception error)
        {
            CoopCommandResult restored = Restore();
            return Result(false, "staging failed: " + error.Message, new { restore = restored.Output, candidateEvidence });
        }
        return Result(true, "ready: speak to the named caravan through the normal client UI", new
        {
            token = observation.Token, player = Describe(player), lord = Describe(lord), caravan = Describe(caravan),
            interactionRangeVerified = true, distance = player.Position.Distance(caravan.Position), candidateEvidence,
            before = Command("before", lord.Position, CampaignTime.Now.NumTicks),
            during = Command("during", lord.Position, CampaignTime.Now.NumTicks),
            released = Command("released", lord.Position, CampaignTime.Now.NumTicks),
            restore = "coop.debug.mobileparty.clan_lord_fixture_restore"
        });
    }

    public CoopCommandResult Observe(IReadOnlyList<string> args)
    {
        if (!CheckCampaign()) return Result(false, "campaign is unavailable");
        if (!TryNumber(args[4], out float tx) || !TryNumber(args[5], out float ty) ||
            !TryNumber(args[6], out float bx) || !TryNumber(args[7], out float by) ||
            !long.TryParse(args[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
            return Result(false, "use the exact numeric observation arguments printed by the fixture");
        if (!objects.TryGetObject(args[1], out MobileParty player) ||
            !objects.TryGetObject(args[2], out MobileParty lord) ||
            !objects.TryGetObject(args[3], out MobileParty caravan))
            return Result(false, "a required exact registry id did not resolve");
        if (!player.IsPlayerParty() || player.LeaderHero?.Clan == null || lord.LeaderHero?.Clan != player.LeaderHero?.Clan || !lord.IsLordParty || !caravan.IsCaravan)
            return Result(false, "player/clan/lord/caravan identity changed");
        if (ModInformation.IsClient && (observation == null ||
            ((args[0] == "before" || args[0] == "state") && observation.Token != args[9])))
            observation = new Observation(args[9], args[1], args[2], args[3], new CampaignVec2(new Vec2(tx, ty), true));
        if (observation == null || !observation.Matches(args, tx, ty))
            return Result(false, "observation does not match this session's fixture token, ids, or target");
        bool conversationActive = campaign.ConversationManager?.IsConversationInProgress == true;
        bool hasPlayerEncounter = PlayerEncounter.Current != null;
        bool exactEncounter = PlayerEncounter.EncounteredParty == caravan.Party;
        var tracker = ConversationPartyTracker.Instance;
        bool held = tracker?.TryGetEngagement(Id(caravan.Party), out _) == true;
        bool exactHold = tracker != null && tracker.TryGetEngagement(Id(caravan.Party), out var engagement) &&
            engagement.EngagerPartyId == Id(player.Party);
        string phase = args[0];
        string failure = null;
        if (phase != "state" && (Unavailable(player) != null || Unavailable(lord) != null || Unavailable(caravan) != null))
            failure = "fixture party unavailable: player=" + Unavailable(player) + "; lord=" + Unavailable(lord) + "; caravan=" + Unavailable(caravan);
        else if (phase != "state" && ModInformation.IsClient && MobileParty.MainParty != player)
            failure = "passive clients use the state phase; during/released/verify belong to the participating client";
        else if (phase == "during")
        {
            failure = observation.ObserveDuring(ModInformation.IsServer ? exactHold :
                conversationActive && hasPlayerEncounter && exactEncounter);
        }
        else if (phase == "released" || phase == "verify")
        {
            if (!observation.During) failure = "the during checkpoint was never observed on this machine";
            else if (ModInformation.IsServer ? held || caravan.Ai?.IsDisabled != false : conversationActive || hasPlayerEncounter)
                failure = "the selected conversation has not fully released";
            else if (phase == "released")
            {
                observation.ObserveRelease(lord.Position, CampaignTime.Now.NumTicks);
            }
            else if (!observation.Released || bx != observation.Baseline.X || by != observation.Baseline.Y || ticks != observation.Ticks)
                failure = "use the verify command from this machine's released checkpoint";
            else
                failure = Unavailable(lord) ?? rules.ValidateMovement(ticks, CampaignTime.Now.NumTicks,
                    bx, by, lord.Position.X, lord.Position.Y, tx, ty, lord.MoveTargetPoint.X, lord.MoveTargetPoint.Y,
                    Coherent(lord, observation.Target), lord.ComputeIsWaiting(), lord.Ai.IsDisabled);
        }
        else if (phase != "before" && phase != "state") failure = "unknown observation phase";
        return Result(failure == null, failure ?? "observation passed", new
        {
            phase, token = observation.Token, player = Describe(player), lord = Describe(lord), caravan = Describe(caravan),
            conversationActive, hasPlayerEncounter, exactEncounter, held, exactHold,
            duringObserved = observation.During, releaseObserved = observation.Released,
            baseline = new { x = observation.Baseline.X, y = observation.Baseline.Y, ticks = observation.Ticks },
            verify = observation.Released ? Command("verify", observation.Baseline, observation.Ticks) : null,
            state = Command("state", lord.Position, CampaignTime.Now.NumTicks)
        });
    }

    public CoopCommandResult Restore()
    {
        if (ModInformation.IsClient) return Result(false, "restore requires the server");
        if (!CheckCampaign()) return Result(false, "campaign is unavailable");
        if (lordCapture == null && caravanCapture == null) return Result(true, "already restored; no active capture");
        if (IsHeld(lordCapture?.Party) || IsHeld(caravanCapture?.Party))
            return Result(false, "exit the conversation normally before restoring");
        var failures = new List<string>();
        RestoreCapture(lordCapture, failures);
        RestoreCapture(caravanCapture, failures);
        if (failures.Count > 0) return Result(false, "restore incomplete; capture retained, retry restore", failures);
        lordCapture = null;
        caravanCapture = null;
        observation = null;
        return Result(true, "lord and caravan restored and final behavior published");
    }

    private void RestoreCapture(Capture capture, List<string> failures)
    {
        if (capture == null || !capture.Modified || capture.Restored) return;
        try
        {
            MobileParty party = capture.Party;
            string failure = Unavailable(party);
            if (failure != null || !objects.TryGetObject(Id(party), out MobileParty current) || current != party)
                throw new InvalidOperationException("party unavailable: " + failure);
            if (!snapshots.TryApply(party, capture.Behavior, out _))
                throw new InvalidOperationException("original behavior references no longer resolve");
            party.Position = capture.Behavior.PartyPosition;
            party.NextTargetPosition = capture.NextTarget;
            party.Ai.IsDisabled = capture.Disabled;
            party.Ai._enableAgainAtHour = capture.EnableAgain;
            party.Ai.SetDoNotMakeNewDecisions(capture.NoDecisions);
            party.Ai.RethinkAtNextHourlyTick = capture.Rethink;
            party.Ai.DefaultBehaviorNeedsUpdate = capture.NeedsUpdate;
            if (!snapshots.TryCreate(party, out var actual) || !SameBehavior(actual, capture.Behavior) ||
                party.NextTargetPosition != capture.NextTarget || party.Ai.IsDisabled != capture.Disabled ||
                party.Ai._enableAgainAtHour != capture.EnableAgain || party.Ai.DoNotMakeNewDecisions != capture.NoDecisions ||
                party.Ai.RethinkAtNextHourlyTick != capture.Rethink || party.Ai.DefaultBehaviorNeedsUpdate != capture.NeedsUpdate)
                throw new InvalidOperationException("restored behavior verification failed");
            Publish(party);
            capture.Restored = true;
        }
        catch (Exception error) { failures.Add(capture.Party.StringId + ": " + error.Message); }
    }

    private bool CheckCampaign()
    {
        if (!ReferenceEquals(campaign, Campaign.Current))
        {
            campaign = Campaign.Current;
            lordCapture = null;
            caravanCapture = null;
            observation = null;
        }
        return campaign?.CampaignObjectManager != null;
    }

    private string EligibleAi(MobileParty party) => Unavailable(party) ??
        (party.IsPlayerParty() ? "player controlled" : party.Ai.IsDisabled ? "AI already disabled" :
        Id(party) == null || Id(party.Party) == null || (party.IsLordParty && Id(party.LeaderHero) == null) ? "identity not registered" :
        party.MoveTargetParty != null && Id(party.MoveTargetParty) == null ? "movement target not registered" :
        IsHeld(party) ? "already in a conversation" : null);

    private string Unavailable(MobileParty party) => party == null ? "missing party" :
        !party.IsActive ? "inactive" : party.Ai == null ? "missing AI" :
        party.CurrentSettlement != null ? "in settlement" : party.MapEvent != null ? "in map event" :
        party.Army != null ? "in army" : party.LeaderHero?.IsPrisoner == true ? "leader captive" :
        party.IsLordParty && party.LeaderHero?.IsActive != true ? "lord inactive" :
        party.IsTransitionInProgress ? "navigation transition" :
        party.IsCurrentlyAtSea || !party.Position.IsOnLand ? "not on land" :
        !party.Position.Face.IsValid() ? "invalid map face" : null;

    private bool IsHeld(MobileParty party) => party != null && ConversationPartyTracker.Instance != null &&
        ConversationPartyTracker.Instance.TryGetEngagement(Id(party.Party), out _);

    private string Id(object value) => value != null && objects.TryGetId(value, out string id) ? id : null;

    private object Describe(MobileParty party) => new
    {
        id = Id(party), partyBaseId = Id(party.Party), stringId = party.StringId, name = party.Name?.ToString(),
        heroId = Id(party.LeaderHero), heroStringId = party.LeaderHero?.StringId, heroName = party.LeaderHero?.Name?.ToString(),
        clanId = Id(party.LeaderHero?.Clan), clanStringId = party.LeaderHero?.Clan?.StringId,
        x = party.Position.X, y = party.Position.Y, targetX = party.MoveTargetPoint.X, targetY = party.MoveTargetPoint.Y,
        defaultBehavior = party.DefaultBehavior.ToString(), shortBehavior = party.ShortTermBehavior.ToString(),
        moveMode = party.PartyMoveMode.ToString(), targetParty = Id(party.TargetParty), targetSettlement = Id(party.TargetSettlement),
        moveTargetParty = Id(party.MoveTargetParty), moving = party.IsMoving, waiting = party.Ai == null ? (bool?)null : party.ComputeIsWaiting(),
        aiDisabled = party.Ai?.IsDisabled, noDecisions = party.Ai?.DoNotMakeNewDecisions,
        rethink = party.Ai?.RethinkAtNextHourlyTick, unavailable = Unavailable(party)
    };

    private bool TryPoint(CampaignVec2 center, float[] radii, out CampaignVec2 point)
    {
        var navigation = MobileParty.NavigationType.Default;
        int[] excluded = campaign.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(navigation);
        foreach (float radius in radii)
        {
            for (int direction = 0; direction < 16; direction++)
            {
                double angle = (Math.PI * direction) / 8;
                var candidate = new CampaignVec2(new Vec2(center.X + ((float)Math.Cos(angle) * radius),
                    center.Y + ((float)Math.Sin(angle) * radius)), true);
                if (!candidate.Face.IsValid() || !NavigationHelper.IsPositionValidForNavigationType(candidate, navigation)) continue;
                if (!campaign.MapSceneWrapper.GetPathDistanceBetweenAIFaces(center.Face, candidate.Face,
                    center.ToVec2(), candidate.ToVec2(), 0.3f, radius * 2f, out _, excluded,
                    campaign.Models.MapDistanceModel.RegionSwitchCostFromLandToSea,
                    campaign.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand)) continue;
                point = candidate;
                return true;
            }
        }
        point = CampaignVec2.Invalid;
        return false;
    }

    private bool Coherent(MobileParty party, CampaignVec2 target) =>
        party.DefaultBehavior == AiBehavior.GoToPoint && party.ShortTermBehavior == AiBehavior.GoToPoint &&
        party.PartyMoveMode == MoveModeType.Point && party.TargetParty == null && party.TargetSettlement == null &&
        party.MoveTargetParty == null && (ModInformation.IsClient || party.Ai.DoNotMakeNewDecisions) &&
        party.TargetPosition.DistanceSquared(target) <= 0.0001f;

    private void Publish(MobileParty party)
    {
        if (!snapshots.TryCreate(party, out var data)) throw new InvalidOperationException("unable to publish final party behavior");
        data.ForcePosition = true;
        messages.Publish(this, new PartyBehaviorUpdated(ref data));
    }

    private string Command(string phase, CampaignVec2 baseline, long ticks) =>
        string.Join(" ", "coop.debug.mobileparty.clan_lord_fixture_observe", phase, observation.PlayerId,
            observation.LordId, observation.CaravanId, Number(observation.Target.X), Number(observation.Target.Y),
            Number(baseline.X), Number(baseline.Y), ticks.ToString(CultureInfo.InvariantCulture), observation.Token);

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static bool TryNumber(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

    private CoopCommandResult Result(bool success, string message, object data = null) => new CoopCommandResult(success,
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            issue = 3264, success, message, side = ModInformation.IsServer ? "server" : "client",
            assembly = typeof(ClanLordMovementFixture).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            ticks = campaign == null ? (long?)null : CampaignTime.Now.NumTicks, data
        }), success ? null : "fixture_failed");

    private static bool SameBehavior(PartyBehaviorUpdateData a, PartyBehaviorUpdateData b) =>
        a.MobilePartyId == b.MobilePartyId && a.PartyPosition == b.PartyPosition && a.NewAiBehavior == b.NewAiBehavior &&
        a.DefaultBehavior == b.DefaultBehavior && a.PartyMoveMode == b.PartyMoveMode && a.MoveTargetPoint == b.MoveTargetPoint &&
        a.TargetPosition == b.TargetPosition && a.TargetPartyId == b.TargetPartyId && a.TargetSettlementId == b.TargetSettlementId &&
        a.MoveTargetPartyId == b.MoveTargetPartyId && a.InteractablePointId == b.InteractablePointId &&
        a.BestTargetPoint == b.BestTargetPoint && a.DesiredAiNavigationType == b.DesiredAiNavigationType &&
        a.IsTargetingPort == b.IsTargetingPort && a.IsInteractableAnchor == b.IsInteractableAnchor && a.IsCurrentlyAtSea == b.IsCurrentlyAtSea;

    private sealed class Capture
    {
        public MobileParty Party { get; }
        public PartyBehaviorUpdateData Behavior { get; }
        public CampaignVec2 NextTarget { get; }
        public bool Disabled { get; }
        public CampaignTime EnableAgain { get; }
        public bool NoDecisions { get; }
        public bool Rethink { get; }
        public bool NeedsUpdate { get; }
        public bool Modified { get; set; }
        public bool Restored { get; set; }
        public Capture(MobileParty party, PartyBehaviorUpdateData behavior)
        {
            Party = party;
            Behavior = behavior;
            NextTarget = party.NextTargetPosition;
            Disabled = party.Ai.IsDisabled;
            EnableAgain = party.Ai._enableAgainAtHour;
            NoDecisions = party.Ai.DoNotMakeNewDecisions;
            Rethink = party.Ai.RethinkAtNextHourlyTick;
            NeedsUpdate = party.Ai.DefaultBehaviorNeedsUpdate;
        }
    }

    internal sealed class Observation
    {
        public string Token { get; }
        public string PlayerId { get; }
        public string LordId { get; }
        public string CaravanId { get; }
        public CampaignVec2 Target { get; }
        public bool During { get; private set; }
        public bool Released { get; private set; }
        public CampaignVec2 Baseline { get; private set; }
        public long Ticks { get; private set; }
        public Observation(string token, string playerId, string lordId, string caravanId, CampaignVec2 target)
        {
            Token = token;
            PlayerId = playerId;
            LordId = lordId;
            CaravanId = caravanId;
            Target = target;
        }
        public string ObserveDuring(bool actualConversation)
        {
            if (Released) return "conversation release already recorded; restore and stage a new fixture";
            if (!actualConversation) return "the selected player's actual conversation with the exact caravan is not active";
            During = true;
            return null;
        }
        public string ObserveRelease(CampaignVec2 position, long ticks)
        {
            if (!During) return "the during checkpoint was never observed on this machine";
            if (!Released)
            {
                Baseline = position;
                Ticks = ticks;
                Released = true;
            }
            return null;
        }
        public bool Matches(IReadOnlyList<string> args, float tx, float ty) => args[1] == PlayerId && args[2] == LordId &&
            args[3] == CaravanId && args[9] == Token && tx == Target.X && ty == Target.Y;
    }
}
#endif
