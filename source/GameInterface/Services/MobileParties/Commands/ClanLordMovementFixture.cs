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

/// <summary>Stages and exercises the clan-lord conversation regression fixture.</summary>
internal interface IClanLordMovementFixture
{
    CoopCommandResult Setup(string playerId);
    CoopCommandResult Observe(IReadOnlyList<string> args);
    CoopCommandResult Start(IReadOnlyList<string> args);
    CoopCommandResult Finish(IReadOnlyList<string> args);
    CoopCommandResult Restore();
}

/// <summary>Owns one session-scoped fixture and retains captured state until restoration succeeds.</summary>
internal sealed class ClanLordMovementFixture : IClanLordMovementFixture
{
    private const int InteractionEvidenceLimit = 32;

    private readonly IObjectManager objects;
    private readonly IMobilePartyBehaviorSnapshot snapshots;
    private readonly IMessageBroker messages;
    private readonly IClanLordMovementFixtureRules rules;
    private Campaign campaign;
    private Capture lordCapture;
    private Capture interactionCapture;
    private FixtureLord fixtureLord;
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
        if (lordCapture != null || interactionCapture != null || fixtureLord != null)
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
        float interactionRange = campaign.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty;
        if (!rules.IsWithinInteractionRange(0f, interactionRange))
            return Result(false, "land interaction range is unavailable; nothing changed");
        if (lord == null)
        {
            string preparationFailure = PrepareFixtureLord(player, clan, out lord);
            if (preparationFailure != null) return Result(false, preparationFailure, candidateEvidence);
        }
        var interactionCandidates = MobileParty.All.Where(p => p != player && p != lord)
            .Select(p => new { Party = p, Rejected = EligibleInteraction(p, player, lord) }).ToArray();
        var interactionEvidence = interactionCandidates.OrderBy(candidate => candidate.Party.StringId, StringComparer.Ordinal)
            .Take(InteractionEvidenceLimit)
            .Select(candidate => new { party = Describe(candidate.Party), rejected = candidate.Rejected }).ToArray();
        bool interactionEvidenceTruncated = interactionCandidates.Length > interactionEvidence.Length;
        MobileParty interaction = interactionCandidates.Where(candidate => candidate.Rejected == null)
            .Select(candidate => candidate.Party)
            .OrderBy(p => rules.InteractionPriority(p.IsCaravan, p.IsLordParty))
            .ThenBy(p => p.Position.DistanceSquared(player.Position))
            .ThenBy(p => p.StringId, StringComparer.Ordinal).FirstOrDefault();
        if (interaction == null)
            return SetupFailure("no eligible registered peaceful interaction party", new
            {
                candidateEvidence, interactionCandidateCount = interactionCandidates.Length,
                interactionEvidence, interactionEvidenceTruncated
            });
        var selectedInteraction = Describe(interaction);
        var interactionRadii = new[] { interactionRange * 0.6f, interactionRange * 0.4f, interactionRange * 0.2f };
        if (!TryPoint(player.Position, interactionRadii, out CampaignVec2 interactionPoint))
            return SetupFailure("no deterministic navigable staging point", new
            {
                candidateEvidence, interaction = selectedInteraction,
                interactionCandidateCount = interactionCandidates.Length,
                interactionEvidence, interactionEvidenceTruncated
            });
        if (!TryPoint(lord.Position, new[] { 24f, 20f, 16f }, out CampaignVec2 target))
            return SetupFailure("no deterministic lord route", new
            {
                candidateEvidence, interaction = selectedInteraction,
                interactionCandidateCount = interactionCandidates.Length,
                interactionEvidence, interactionEvidenceTruncated
            });
        if (!snapshots.TryCreate(lord, out var lordState) || !snapshots.CanApply(lord, lordState) ||
            !snapshots.TryCreate(interaction, out var interactionState) || !snapshots.CanApply(interaction, interactionState))
            return SetupFailure("unable to capture restorable movement state", new
            {
                candidateEvidence, interaction = selectedInteraction,
                interactionCandidateCount = interactionCandidates.Length,
                interactionEvidence, interactionEvidenceTruncated
            });

        lordCapture = new Capture(lord, lordState);
        interactionCapture = new Capture(interaction, interactionState);
        observation = new Observation(Guid.NewGuid().ToString("N"), playerId, Id(lord), Id(interaction), target);
        try
        {
            interactionCapture.Modified = true;
            interaction.Position = interactionPoint;
            interaction.SetMoveModeHold();
            interaction.SetNavigationModeHold();
            interaction.Ai.SetDoNotMakeNewDecisions(true);
            // CanPartyInteract reads the player's current target, which need not be this interaction party.
            if (!rules.IsWithinInteractionRange(player.Position.Distance(interaction.Position), interactionRange))
                throw new InvalidOperationException("staged interaction party is outside the real vanilla interaction range");
            lordCapture.Modified = true;
            lord.Ai.SetDoNotMakeNewDecisions(true);
            lord.SetMoveGoToPoint(target, MobileParty.NavigationType.Default);
            lord.SetShortTermBehavior(AiBehavior.GoToPoint, null);
            lord.Ai.BehaviorTarget = target;
            lord.SetNavigationModePoint(target);
            Publish(lord);
            Publish(interaction);
        }
        catch (Exception error)
        {
            return SetupFailure("staging failed: " + error.Message, new
            {
                candidateEvidence, interaction = selectedInteraction,
                interactionCandidateCount = interactionCandidates.Length,
                interactionEvidence, interactionEvidenceTruncated
            });
        }
        return Result(true, "ready: run the printed source-bound start command on the participating client", new
        {
            token = observation.Token, player = Describe(player), lord = Describe(lord), interaction = selectedInteraction,
            preparedLord = fixtureLord != null,
            originalClan = fixtureLord == null ? null : new { id = Id(fixtureLord.OriginalClan), stringId = fixtureLord.OriginalClan.StringId },
            interactionRangeVerified = true, interactionRange,
            distance = player.Position.Distance(interaction.Position), candidateEvidence,
            interactionCandidateCount = interactionCandidates.Length, interactionEvidence, interactionEvidenceTruncated,
            before = Command("before", lord.Position, CampaignTime.Now.NumTicks),
            start = Command("clan_lord_fixture_start", "before", lord.Position, CampaignTime.Now.NumTicks),
            during = Command("during", lord.Position, CampaignTime.Now.NumTicks),
            finish = Command("clan_lord_fixture_finish", "during", lord.Position, CampaignTime.Now.NumTicks),
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
            !objects.TryGetObject(args[3], out MobileParty interaction))
            return Result(false, "a required exact registry id did not resolve");
        if (!player.IsPlayerParty() || player.LeaderHero?.Clan == null || lord.LeaderHero?.Clan != player.LeaderHero?.Clan ||
            !lord.IsLordParty || interaction == player || interaction == lord || interaction.IsPlayerParty())
            return Result(false, "player/clan/lord/interaction identity changed");
        if (ModInformation.IsClient && (observation == null ||
            ((args[0] == "before" || args[0] == "state") && observation.Token != args[9])))
            observation = new Observation(args[9], args[1], args[2], args[3], new CampaignVec2(new Vec2(tx, ty), true));
        if (observation == null || !observation.Matches(args, tx, ty))
            return Result(false, "observation does not match this session's fixture token, ids, or target");
        bool conversationActive = campaign.ConversationManager?.IsConversationInProgress == true;
        bool hasPlayerEncounter = PlayerEncounter.Current != null;
        bool exactEncounter = PlayerEncounter.EncounteredParty == interaction.Party;
        var tracker = ConversationPartyTracker.Instance;
        bool held = tracker?.TryGetEngagement(Id(interaction.Party), out _) == true;
        bool exactHold = tracker != null && tracker.TryGetEngagement(Id(interaction.Party), out var engagement) &&
            engagement.EngagerPartyId == Id(player.Party);
        string phase = args[0];
        string failure = null;
        if (phase != "state" && (Unavailable(player) != null || Unavailable(lord) != null || Unavailable(interaction) != null))
            failure = "fixture party unavailable: player=" + Unavailable(player) + "; lord=" + Unavailable(lord) + "; interaction=" + Unavailable(interaction);
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
            else if (ModInformation.IsServer ? held || interaction.Ai?.IsDisabled != false : conversationActive || hasPlayerEncounter)
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
            phase, token = observation.Token, player = Describe(player), lord = Describe(lord), interaction = Describe(interaction),
            conversationActive, hasPlayerEncounter, exactEncounter, held, exactHold,
            duringObserved = observation.During, releaseObserved = observation.Released,
            baseline = new { x = observation.Baseline.X, y = observation.Baseline.Y, ticks = observation.Ticks },
            verify = observation.Released ? Command("verify", observation.Baseline, observation.Ticks) : null,
            state = Command("state", lord.Position, CampaignTime.Now.NumTicks)
        });
    }

    public CoopCommandResult Start(IReadOnlyList<string> args)
    {
        if (ModInformation.IsServer) return Result(false, "start requires the participating client");
        if (args[0] != "before") return Result(false, "use the exact start command printed by the fixture");
        CoopCommandResult before = Observe(args);
        if (!before.Succeeded) return before;
        if (!objects.TryGetObject(args[1], out MobileParty player) || !objects.TryGetObject(args[3], out MobileParty interaction))
            return Result(false, "a required exact registry id did not resolve");
        if (MobileParty.MainParty != player) return Result(false, "start requires the participating player client");
        if (PlayerEncounter.Current != null) return Result(false, "finish the current player encounter before starting the fixture conversation");
        float interactionRange = campaign.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty;
        if (!rules.IsWithinInteractionRange(player.Position.Distance(interaction.Position), interactionRange))
            return Result(false, "the staged interaction party is outside the real vanilla interaction range");

        EncounterManager.StartPartyEncounter(player.Party, interaction.Party);
        return Result(true, "requested the exact interaction party through the production encounter action", new
        {
            player = Describe(player), interaction = Describe(interaction), interactionRange
        });
    }

    public CoopCommandResult Finish(IReadOnlyList<string> args)
    {
        if (ModInformation.IsServer) return Result(false, "finish requires the participating client");
        if (args[0] != "during") return Result(false, "use the exact finish command printed by the fixture");
        CoopCommandResult during = Observe(args);
        if (!during.Succeeded) return during;
        if (!objects.TryGetObject(args[3], out MobileParty interaction) || PlayerEncounter.EncounteredParty != interaction.Party)
            return Result(false, "the exact staged interaction party is no longer the active player encounter");

        PlayerEncounter.Finish();
        return Result(true, "finished the exact interaction party through the production encounter action", new { interaction = Describe(interaction) });
    }

    public CoopCommandResult Restore()
    {
        if (ModInformation.IsClient) return Result(false, "restore requires the server");
        if (!CheckCampaign()) return Result(false, "campaign is unavailable");
        if (lordCapture == null && interactionCapture == null && fixtureLord == null)
            return Result(true, "already restored; no active capture");
        if (IsHeld(lordCapture?.Party) || IsHeld(interactionCapture?.Party) || IsHeld(fixtureLord?.Party))
            return Result(false, "exit the conversation normally before restoring");
        bool restoreFixtureLord = fixtureLord != null;
        var failures = new List<string>();
        RestoreCapture(lordCapture, failures);
        RestoreCapture(interactionCapture, failures);
        RestoreFixtureLord(failures);
        if (failures.Count > 0) return Result(false, "restore incomplete; capture retained, retry restore", failures);
        lordCapture = null;
        interactionCapture = null;
        fixtureLord = null;
        observation = null;
        return Result(true, restoreFixtureLord
            ? "lord, interaction party, and prepared clan assignment restored"
            : "lord and interaction party restored and final behavior published");
    }

    private string PrepareFixtureLord(MobileParty player, Clan clan, out MobileParty lord)
    {
        lord = null;
        MobileParty party = MobileParty.All.Where(p => p.IsLordParty && p != player && !p.IsPlayerParty() &&
                p.LeaderHero?.Clan != null && p.LeaderHero.Clan.Leader != p.LeaderHero && p.LeaderHero.Clan != clan &&
                p.LeaderHero.CompanionOf == null && EligibleAi(p) == null)
            .OrderBy(p => p.LeaderHero.StringId, StringComparer.Ordinal)
            .ThenBy(p => p.StringId, StringComparer.Ordinal).FirstOrDefault();
        if (party == null)
            return "no eligible registered non-player lord is available to prepare for the player's clan";
        try
        {
            Hero hero = party.LeaderHero;
            fixtureLord = new FixtureLord(party, hero, hero.Clan);
            hero.Clan = clan;
            if (hero.Clan != clan)
                throw new InvalidOperationException("existing lord was not prepared in the player's clan");
            lord = party;
            return null;
        }
        catch (Exception error)
        {
            CoopCommandResult restored = Restore();
            return "unable to prepare an existing fixture lord: " + error.Message + "; " + restored.Output;
        }
    }

    private CoopCommandResult SetupFailure(string message, object candidateEvidence)
    {
        CoopCommandResult restored = Restore();
        return Result(false, message, new { restore = restored.Output, candidateEvidence });
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

    private void RestoreFixtureLord(List<string> failures)
    {
        if (fixtureLord == null) return;
        try
        {
            if (fixtureLord.Hero.Clan != fixtureLord.OriginalClan)
                fixtureLord.Hero.Clan = fixtureLord.OriginalClan;
            if (fixtureLord.Hero.Clan != fixtureLord.OriginalClan)
                throw new InvalidOperationException("original clan was not restored");
        }
        catch (Exception error) { failures.Add("prepared fixture lord: " + error.Message); }
    }

    private bool CheckCampaign()
    {
        if (!ReferenceEquals(campaign, Campaign.Current))
        {
            campaign = Campaign.Current;
            lordCapture = null;
            interactionCapture = null;
            fixtureLord = null;
            observation = null;
        }
        return campaign?.CampaignObjectManager != null;
    }

    private string EligibleAi(MobileParty party) => Unavailable(party) ??
        (party.IsPlayerParty() ? "player controlled" : party.Ai.IsDisabled ? "AI already disabled" :
        Id(party) == null || Id(party.Party) == null || (party.IsLordParty && Id(party.LeaderHero) == null) ? "identity not registered" :
        party.MoveTargetParty != null && Id(party.MoveTargetParty) == null ? "movement target not registered" :
        IsHeld(party) ? "already in a conversation" : null);

    private string EligibleInteraction(MobileParty party, MobileParty player, MobileParty lord) => EligibleAi(party) ??
        (party == lord ? "selected lord" : party.ShouldBeIgnored ? "ignored" :
        party.IsLordParty && party.LeaderHero?.Clan?.Leader == party.LeaderHero ? "clan leader" :
        party.MapFaction == null ? "missing map faction" : player.MapFaction == null ? "player missing map faction" :
        party.MapFaction.IsAtWarWith(player.MapFaction) ? "at war with player" : null);

    private string Unavailable(MobileParty party) => party == null ? "missing party" :
        !party.IsActive ? "inactive" : party.Ai == null ? "missing AI" :
        party.CurrentSettlement != null ? "in settlement" : party.MapEvent != null ? "in map event" :
        party.BesiegerCamp != null ? "in siege" :
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
        Command("clan_lord_fixture_observe", phase, baseline, ticks);

    private string Command(string name, string phase, CampaignVec2 baseline, long ticks) =>
        string.Join(" ", "coop.debug.mobileparty." + name, phase, observation.PlayerId,
            observation.LordId, observation.InteractionId, Number(observation.Target.X), Number(observation.Target.Y),
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

    private sealed class FixtureLord
    {
        public MobileParty Party { get; }
        public Hero Hero { get; }
        public Clan OriginalClan { get; }
        public FixtureLord(MobileParty party, Hero hero, Clan originalClan)
        {
            Party = party;
            Hero = hero;
            OriginalClan = originalClan;
        }
    }

    internal sealed class Observation
    {
        public string Token { get; }
        public string PlayerId { get; }
        public string LordId { get; }
        public string InteractionId { get; }
        public CampaignVec2 Target { get; }
        public bool During { get; private set; }
        public bool Released { get; private set; }
        public CampaignVec2 Baseline { get; private set; }
        public long Ticks { get; private set; }
        public Observation(string token, string playerId, string lordId, string interactionId, CampaignVec2 target)
        {
            Token = token;
            PlayerId = playerId;
            LordId = lordId;
            InteractionId = interactionId;
            Target = target;
        }
        public string ObserveDuring(bool actualConversation)
        {
            if (Released) return "conversation release already recorded; restore and stage a new fixture";
            if (!actualConversation) return "the selected player's actual conversation with the exact interaction party is not active";
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
            args[3] == InteractionId && args[9] == Token && tx == Target.X && ty == Target.Y;
    }
}
#endif
