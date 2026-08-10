#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Auto;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Missions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Commands;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands;

/// <summary>
/// DEBUG-only live fixture for #2703. It uses a temporary player kingdom so the actual diplomacy,
/// hostile-encounter, and inbound-peace paths can be observed without changing the loaded save.
/// </summary>
public class WarAndPeaceReproductionFixtureCommands
{
    private const string AseraiKingdomId = "aserai";
    private static WarAndPeaceFixture activeFixture;

    [CommandLineArgumentFunction("war_peace_fixture_preflight", "coop.debug.kingdom")]
    public static string Preflight(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.war_peace_fixture_preflight <controllerId>";
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (activeFixture != null) return "A war and peace fixture is already active.";

        if (!TryResolveFixtureInputs(args[0], out var player, out var playerClan, out var playerParty,
                out var aserai, out var caravan, out string error))
        {
            return error;
        }

        bool valid = playerClan.Kingdom == null &&
                     playerClan.Leader != null &&
                     playerClan.Culture != null &&
                     playerParty.CurrentSettlement == null &&
                     playerParty.MapEvent == null &&
                     (caravan == null || (caravan.MapEvent == null && caravan.CurrentSettlement == null)) &&
                     (caravan != null || TryGetTemporaryCaravanInputs(
                         aserai,
                         out _,
                         out _,
                         out _,
                         out _)) &&
                     Kingdom.All.Sum(kingdom => kingdom?._unresolvedDecisions?.Count ?? 0) == 0;
        return $"WAR_PEACE_FIXTURE_PREFLIGHT valid={Bool(valid)} controller={player.ControllerId} " +
               $"playerClan={playerClan.StringId} playerParty={playerParty.StringId} " +
               $"playerKingdom={playerClan.Kingdom?.StringId ?? "none"} target={aserai.StringId} " +
               $"caravan={caravan?.StringId ?? "create-on-prepare"} " +
               $"caravanCreatedOnPrepare={Bool(caravan == null)} " +
               $"playerMapEvent={Bool(playerParty.MapEvent != null)} " +
               $"caravanMapEvent={Bool(caravan?.MapEvent != null)} " +
               $"unresolvedDecisionCount={Kingdom.All.Sum(kingdom => kingdom?._unresolvedDecisions?.Count ?? 0)}";
    }

    [CommandLineArgumentFunction("war_peace_fixture_prepare", "coop.debug.kingdom")]
    public static string Prepare(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.war_peace_fixture_prepare <controllerId>";
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (activeFixture != null) return "A war and peace fixture is already active.";
        if (!ContainerProvider.TryResolve(out IKingdomCreator kingdomCreator) ||
            !ContainerProvider.TryResolve(out IKingdomMembershipState kingdomMembershipState))
        {
            return "Unable to resolve the kingdom fixture services.";
        }
        if (!TryResolveFixtureInputs(args[0], out var player, out var playerClan, out var playerParty,
                out var aserai, out var caravan, out string error))
        {
            return error;
        }
        if (playerClan.Kingdom != null)
        {
            return $"Player clan {playerClan.StringId} already belongs to kingdom {playerClan.Kingdom.StringId}.";
        }
        if (playerClan.Leader == null || playerClan.Culture == null)
        {
            return $"Player clan {playerClan.StringId} has no leader or culture.";
        }
        if (playerParty.CurrentSettlement != null || playerParty.MapEvent != null ||
            caravan?.CurrentSettlement != null || caravan?.MapEvent != null)
        {
            return "The player party and Aserai caravan must both be outside settlements and map events.";
        }

        bool caravanCreated = false;
        if (caravan == null)
        {
            if (!TryCreateTemporaryAseraiCaravan(aserai, playerParty, out caravan, out error))
            {
                return error;
            }
            caravanCreated = true;
        }

        float playerInfluence = playerClan.Influence;
        bool playerClanWasAtWar = AreAtWar(playerClan, aserai);
        var kingdomsBeforeCreation = new HashSet<Kingdom>(Kingdom.All);
        if (!kingdomCreator.TryCreateKingdom(
                playerClan,
                "2703 War And Peace Fixture",
                playerClan.Culture,
                player.ControllerId,
                out string kingdomId,
                out string createError))
        {
            bool caravanCleanupPassed = !caravanCreated || DestroyTemporaryCaravan(caravan);
            return $"Unable to create the war and peace fixture kingdom: {createError}. " +
                   $"Temporary caravan cleanup passed: {Bool(caravanCleanupPassed)}.";
        }
        Kingdom kingdom = null;
        bool kingdomWasResolved = ContainerProvider.TryResolve(out IObjectManager objectManager) &&
                                  objectManager.TryGetObject(kingdomId, out kingdom);
        if (kingdom == null)
        {
            kingdom = playerClan.Kingdom ?? Kingdom.All.FirstOrDefault(candidate =>
                !kingdomsBeforeCreation.Contains(candidate) && candidate.Clans.Contains(playerClan));
        }
        if (kingdom == null)
        {
            return $"Created fixture kingdom {kingdomId} was neither registered nor assigned to the player clan.";
        }

        activeFixture = new WarAndPeaceFixture(
            player.ControllerId,
            playerClan,
            playerParty,
            playerInfluence,
            playerClanWasAtWar,
            kingdom,
            kingdomId,
            aserai,
            caravan,
            caravanCreated,
            kingdomMembershipState);

        if (!kingdomWasResolved)
        {
            string cleanup = RestoreFixture();
            return $"Created fixture kingdom {kingdomId} was not registered. {cleanup}";
        }

        try
        {
            float influenceTarget = Math.Max(playerClan.Influence, 5000f);
            if (influenceTarget > playerClan.Influence)
            {
                ChangeClanInfluenceAction.Apply(playerClan, influenceTarget - playerClan.Influence);
            }

            return $"WAR_PEACE_FIXTURE_READY controller={player.ControllerId} kingdom={kingdomId} " +
                   $"playerClan={playerClan.StringId} playerParty={playerParty.StringId} " +
                   $"target={aserai.StringId} caravan={caravan.StringId} " +
                   $"caravanCreated={Bool(caravanCreated)}";
        }
        catch (Exception e)
        {
            string cleanup = RestoreFixture();
            return $"War and peace fixture preparation failed: {e.GetType().Name}: {e.Message}. {cleanup}";
        }
    }

    [CommandLineArgumentFunction("war_peace_fixture_open_war", "coop.debug.kingdom")]
    public static string OpenWarScreen(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.war_peace_fixture_open_war <fixtureKingdomId>";
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetClientFixtureKingdom(args[0], out var kingdom, out string error)) return error;
        if (Clan.PlayerClan?.Kingdom != kingdom)
        {
            return $"The local player clan is not in fixture kingdom {args[0]}.";
        }
        if (!TryGetAserai(out var aserai)) return $"Unable to resolve kingdom {AseraiKingdomId}.";
        if (Game.Current?.GameStateManager == null) return "The game-state manager is unavailable.";
        if (Game.Current.GameStateManager.ActiveState is KingdomState)
        {
            return "The Kingdom screen is already open.";
        }

        Game.Current.GameStateManager.PushState(
            Game.Current.GameStateManager.CreateState<KingdomState>(aserai), 0);
        return "WAR_PEACE_FIXTURE_WAR_SCREEN_OPENING";
    }

    [CommandLineArgumentFunction("war_peace_fixture_start_war", "coop.debug.kingdom")]
    public static string StartWarProposal(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_start_war";
        if (!TryGetAserai(out var aserai)) return $"Unable to resolve kingdom {AseraiKingdomId}.";

        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        KingdomDiplomacyVM diplomacy = kingdomScreen?.DataSource?.Diplomacy;
        if (diplomacy == null) return "The Kingdom diplomacy screen is not ready.";
        if (Clan.PlayerClan?.Kingdom == null) return "The local player clan does not belong to a kingdom.";
        if (Clan.PlayerClan.Kingdom.IsAtWarWith(aserai)) return "The fixture kingdom is already at war with Aserai.";

        diplomacy.SelectKingdom(aserai);
        KingdomDiplomacyProposalActionItemVM warAction = FindWarProposalAction(diplomacy, aserai);
        if (warAction == null) return "The real Aserai war proposal action was not available in the Kingdom screen.";
        if (!warAction.IsEnabled)
        {
            return $"WAR_PEACE_FIXTURE_WAR_ACTION_DISABLED hint={warAction.Hint?.HintText?.ToString() ?? "none"}";
        }

        string actionName = warAction.Name;
        warAction.ExecuteAction();
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        bool expectedDecision = decisionItem?.KingdomDecisionMaker?._decision is DeclareWarDecision decision &&
                                decision.FactionToDeclareWarOn == aserai;
        return $"WAR_PEACE_FIXTURE_WAR_ACTION_EXECUTED actionName={actionName} " +
               $"decisionOpen={Bool(expectedDecision)}";
    }

    [CommandLineArgumentFunction("war_peace_fixture_select_war_yes", "coop.debug.kingdom")]
    public static string SelectWarYes(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_select_war_yes";
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        DecisionOptionVM yesOption = decisionItem?.DecisionOptionsList.FirstOrDefault(option =>
            option.Option is DeclareWarDecision.DeclareWarDecisionOutcome { ShouldWarBeDeclared: true });
        if (yesOption == null) return "The active decision does not have a Yes war option.";

        yesOption.ExecuteSelection();
        return decisionItem._currentSelectedOption == yesOption && decisionItem.CanEndDecision
            ? "WAR_PEACE_FIXTURE_WAR_YES_SELECTED"
            : "The Yes war option did not become finalizable.";
    }

    [CommandLineArgumentFunction("war_peace_fixture_submit", "coop.debug.kingdom")]
    public static string SubmitFinalSelection(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_submit";
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        if (decisionItem == null || !decisionItem.CanEndDecision)
        {
            return "No finalizable kingdom decision item.";
        }

        decisionItem.ExecuteFinalSelection();
        return "WAR_PEACE_FIXTURE_FINAL_SELECTION_EXECUTED";
    }

    [CommandLineArgumentFunction("war_peace_fixture_reset_neutral", "coop.debug.kingdom")]
    public static string ResetNeutral(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_reset_neutral";
        if (activeFixture == null) return "No war and peace fixture is active.";

        try
        {
            FinalizeAttackMapEvent(activeFixture);
            RemoveFixtureDecisions(activeFixture);
            if (AreAtWar(activeFixture.Kingdom, activeFixture.Aserai))
            {
                MakePeaceAction.Apply(activeFixture.Kingdom, activeFixture.Aserai);
            }
            return $"WAR_PEACE_FIXTURE_NEUTRAL warWithAserai={Bool(AreAtWar(activeFixture.Kingdom, activeFixture.Aserai))}";
        }
        catch (Exception e)
        {
            return $"Unable to reset the fixture to neutral: {e.GetType().Name}: {e.Message}";
        }
    }

    [CommandLineArgumentFunction("war_peace_fixture_attack_caravan", "coop.debug.kingdom")]
    public static string AttackCaravan(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_attack_caravan";
        if (activeFixture == null) return "No war and peace fixture is active.";
        if (!ContainerProvider.TryResolve(out INetwork network) ||
            !ContainerProvider.TryResolve(out IObjectManager objectManager) ||
            !ContainerProvider.TryResolve(out IMobilePartyBehaviorSnapshot behaviorSnapshot))
        {
            return "Unable to resolve the neutral encounter fixture services.";
        }
        if (!CanStartAttack(activeFixture, out string error)) return error;
        if (!behaviorSnapshot.TryCreate(activeFixture.PlayerParty, out PartyBehaviorUpdateData playerBehavior) ||
            !behaviorSnapshot.TryCreate(activeFixture.Caravan, out PartyBehaviorUpdateData caravanBehavior))
        {
            return "Unable to capture the original player-party and Aserai-caravan behavior.";
        }
        if (!objectManager.TryGetId(activeFixture.PlayerParty.Party, out string playerPartyId) ||
            !objectManager.TryGetId(activeFixture.Caravan.Party, out string caravanPartyId))
        {
            return "Unable to resolve the player party or Aserai caravan PartyBase id.";
        }

        // Do not declare war here. The client-side Attack! path must be the first hostile action.
        activeFixture.BehaviorSnapshot = behaviorSnapshot;
        activeFixture.PlayerPartyBehavior = playerBehavior;
        activeFixture.CaravanBehavior = caravanBehavior;
        activeFixture.AttackBehaviorCaptured = true;
        activeFixture.AttackBehaviorRestored = false;
        activeFixture.AttackMapEvent = MapEventBattleFactory.CreateMapEvent(
            activeFixture.PlayerParty.Party,
            activeFixture.Caravan.Party,
            default);
        if (activeFixture.AttackMapEvent == null ||
            !objectManager.TryGetId(activeFixture.AttackMapEvent, out string mapEventId))
        {
            if (activeFixture.AttackMapEvent != null && !activeFixture.AttackMapEvent.IsFinalized)
            {
                activeFixture.AttackMapEvent.FinalizeEvent();
            }
            activeFixture.AttackMapEvent = null;
            if (!RestoreAttackPartyBehaviors(activeFixture))
            {
                return "The neutral Aserai caravan map event failed and the original party behavior could not be restored.";
            }
            return "The neutral Aserai caravan map event could not be created and registered.";
        }

        network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
            "2703-war-peace-" + Guid.NewGuid().ToString("N"),
            playerPartyId,
            caravanPartyId,
            mapEventId));
        return $"WAR_PEACE_FIXTURE_NEUTRAL_CARAVAN_ENCOUNTER_STARTED mapEvent={mapEventId} " +
               $"warWithAserai={Bool(AreAtWar(activeFixture.Kingdom, activeFixture.Aserai))}";
    }

    [CommandLineArgumentFunction("war_peace_fixture_run_hourly_tick", "coop.debug.kingdom")]
    public static string RunHourlyTick(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_run_hourly_tick";
        if (activeFixture == null) return "No war and peace fixture is active.";

        DeclareWarDecision before = activeFixture.Kingdom.UnresolvedDecisions
            .OfType<DeclareWarDecision>()
            .FirstOrDefault(decision => decision.FactionToDeclareWarOn == activeFixture.Aserai);
        if (before == null) return "No pending Aserai war decision exists to sweep.";
        int unresolvedDecisionCount = Kingdom.All.Sum(kingdom => kingdom?._unresolvedDecisions?.Count ?? 0);
        if (unresolvedDecisionCount != 1)
        {
            return $"The hourly fixture requires exactly one unresolved decision; found {unresolvedDecisionCount}.";
        }

        bool cancelledBefore = before.ShouldBeCancelled();
        CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();
        DeclareWarDecision after = activeFixture.Kingdom.UnresolvedDecisions
            .OfType<DeclareWarDecision>()
            .FirstOrDefault(decision => decision.FactionToDeclareWarOn == activeFixture.Aserai);
        return $"WAR_PEACE_FIXTURE_HOURLY_TICK warDecisionBefore=true " +
               $"shouldBeCancelled={Bool(cancelledBefore)} warDecisionAfter={Bool(after != null)}";
    }

    [CommandLineArgumentFunction("war_peace_fixture_finalize_attack", "coop.debug.kingdom")]
    public static string FinalizeAttack(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_finalize_attack";
        if (activeFixture == null) return "No war and peace fixture is active.";

        try
        {
            FinalizeAttackMapEvent(activeFixture);
            return "WAR_PEACE_FIXTURE_CARAVAN_ATTACK_FINALIZED";
        }
        catch (Exception e)
        {
            return $"Unable to finalize the caravan attack: {e.GetType().Name}: {e.Message}";
        }
    }

    [CommandLineArgumentFunction("war_peace_fixture_stage_ai_peace", "coop.debug.kingdom")]
    public static string StageAiPeaceOffer(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_stage_ai_peace";
        if (activeFixture == null) return "No war and peace fixture is active.";
        if (!AreAtWar(activeFixture.Kingdom, activeFixture.Aserai))
        {
            return "The fixture kingdom must be at war with Aserai before staging the AI peace offer.";
        }
        if (activeFixture.Aserai.RulingClan == null)
        {
            return "Aserai has no ruling clan to propose peace.";
        }
        if (!ContainerProvider.TryResolve(out IKingdomInterface kingdomInterface))
        {
            return "Unable to resolve the kingdom interface for the real Aserai peace election.";
        }

        var aiDecision = new MakePeaceKingdomDecision(
            activeFixture.Aserai.RulingClan,
            activeFixture.Kingdom,
            dailyTributeToBePaid: -1,
            dailyTributeDurationInDays: 30,
            applyResults: true,
            isProposedByOpponent: false);
        List<Clan> aiSupporters = aiDecision.DetermineSupporters()
            .Select(supporter => supporter.Clan)
            .Where(clan => clan != null)
            .Distinct()
            .ToList();
        if (aiSupporters.Count == 0)
        {
            return "Aserai has no eligible clan supporters for the real peace election.";
        }

        Dictionary<Clan, float> originalInfluence = aiSupporters
            .ToDictionary(clan => clan, clan => clan.Influence);
        try
        {
            // A zero-influence electorate gives vanilla's stable 0-0 tie, whose first candidate is Yes.
            foreach (Clan supporterClan in aiSupporters)
            {
                ChangeClanInfluenceAction.Apply(supporterClan, -supporterClan.Influence);
            }
            kingdomInterface.AddDecision(
                activeFixture.Aserai,
                aiDecision,
                ignoreInfluenceCost: true,
                randomFloat: 0f,
                applyInfluenceCost: false);
        }
        finally
        {
            foreach (KeyValuePair<Clan, float> entry in originalInfluence)
            {
                ChangeClanInfluenceAction.Apply(entry.Key, entry.Value - entry.Key.Influence);
            }
        }

        bool aiDecisionPending = activeFixture.Aserai.UnresolvedDecisions.Contains(aiDecision);
        bool influenceRestored = originalInfluence.All(entry =>
            Math.Abs(entry.Key.Influence - entry.Value) <= 0.001f);
        MakePeaceKingdomDecision inboundOffer = activeFixture.Kingdom.UnresolvedDecisions
            .OfType<MakePeaceKingdomDecision>()
            .FirstOrDefault(decision => decision._isProposedByOpponent &&
                                        decision.FactionToMakePeaceWith == activeFixture.Aserai);
        return $"WAR_PEACE_FIXTURE_AI_PEACE_STAGED chosenPeace={Bool(inboundOffer != null)} " +
               $"aiDecisionPending={Bool(aiDecisionPending)} " +
               $"aiSupporterCount={aiSupporters.Count} influenceRestored={Bool(influenceRestored)} " +
               $"warWithAserai={Bool(AreAtWar(activeFixture.Kingdom, activeFixture.Aserai))} " +
               $"inboundOffer={Bool(inboundOffer != null)}";
    }

    [CommandLineArgumentFunction("war_peace_fixture_open_peace", "coop.debug.kingdom")]
    public static string OpenInboundPeaceOffer(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.war_peace_fixture_open_peace <fixtureKingdomId>";
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetClientFixtureKingdom(args[0], out var kingdom, out string error)) return error;
        MakePeaceKingdomDecision offer = kingdom.UnresolvedDecisions
            .OfType<MakePeaceKingdomDecision>()
            .FirstOrDefault(decision => decision._isProposedByOpponent && decision.FactionToMakePeaceWith?.StringId == AseraiKingdomId);
        if (offer == null) return "No inbound Aserai peace offer is pending.";
        if (Game.Current?.GameStateManager == null) return "The game-state manager is unavailable.";

        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        if (kingdomScreen != null)
        {
            kingdomScreen.DataSource.Decision.HandleDecision(offer);
            return "WAR_PEACE_FIXTURE_INBOUND_PEACE_OPENED";
        }
        if (Game.Current.GameStateManager.ActiveState is KingdomState)
        {
            return "The Kingdom screen is already open.";
        }

        Game.Current.GameStateManager.PushState(
            Game.Current.GameStateManager.CreateState<KingdomState>(offer), 0);
        return "WAR_PEACE_FIXTURE_INBOUND_PEACE_OPENING";
    }

    [CommandLineArgumentFunction("war_peace_fixture_confirm_peace", "coop.debug.kingdom")]
    public static string ConfirmInboundPeaceOffer(List<string> args)
    {
        if (ModInformation.IsServer) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_confirm_peace";

        var decisions = (ScreenManager.TopScreen as GauntletKingdomScreen)?.DataSource?.Decision;
        var inquiry = decisions?._queryData;
        if (inquiry?.AffirmativeAction == null || !inquiry.IsAffirmativeOptionShown)
        {
            return "The inbound peace confirmation inquiry is not ready.";
        }
        if (!TaleWorlds.Library.InformationManager.IsAnyInquiryActive())
        {
            return "The inbound peace confirmation inquiry is not active.";
        }

        inquiry.AffirmativeAction();
        TaleWorlds.Library.InformationManager.HideInquiry();
        DecisionItemBaseVM decisionItem = decisions.CurrentDecision;
        bool expectedDecision = decisionItem?.KingdomDecisionMaker?._decision is MakePeaceKingdomDecision peace &&
                                peace._isProposedByOpponent &&
                                peace.FactionToMakePeaceWith?.StringId == AseraiKingdomId;
        return expectedDecision && decisionItem.IsActive
            ? "WAR_PEACE_FIXTURE_INBOUND_PEACE_CONFIRMED"
            : "The inbound peace decision did not become active.";
    }

    [CommandLineArgumentFunction("war_peace_fixture_select_peace_no", "coop.debug.kingdom")]
    public static string SelectPeaceNo(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_select_peace_no";
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        DecisionOptionVM noOption = decisionItem?.DecisionOptionsList.FirstOrDefault(option =>
            option.Option is MakePeaceKingdomDecision.MakePeaceDecisionOutcome { ShouldPeaceBeDeclared: false });
        if (noOption == null) return "The active decision does not have a No peace option.";

        noOption.ExecuteSelection();
        return decisionItem._currentSelectedOption == noOption && decisionItem.CanEndDecision
            ? "WAR_PEACE_FIXTURE_PEACE_NO_SELECTED"
            : "The No peace option did not become finalizable.";
    }

    [CommandLineArgumentFunction("war_peace_fixture_state", "coop.debug.kingdom")]
    public static string State(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.war_peace_fixture_state <fixtureKingdomId>";
        if (args.Count != 1) return usage;
        if (!ContainerProvider.TryResolve(out IObjectManager objectManager) ||
            !objectManager.TryGetObject(args[0], out Kingdom kingdom))
        {
            return $"Unable to resolve fixture kingdom {args[0]}.";
        }
        if (!TryGetAserai(out var aserai)) return $"Unable to resolve kingdom {AseraiKingdomId}.";

        DeclareWarDecision[] warDecisions = kingdom.UnresolvedDecisions
            .OfType<DeclareWarDecision>()
            .Where(decision => decision.FactionToDeclareWarOn == aserai)
            .ToArray();
        MakePeaceKingdomDecision[] peaceOffers = kingdom.UnresolvedDecisions
            .OfType<MakePeaceKingdomDecision>()
            .Where(decision => decision._isProposedByOpponent && decision.FactionToMakePeaceWith == aserai)
            .ToArray();
        MakePeaceKingdomDecision peaceOffer = peaceOffers.FirstOrDefault();
        DeclareWarDecision warDecision = warDecisions.FirstOrDefault();
        var decisions = (ScreenManager.TopScreen as GauntletKingdomScreen)?.DataSource?.Decision;
        DecisionItemBaseVM decisionItem = decisions?.CurrentDecision;
        bool queryDataPresent = decisions?._queryData?.AffirmativeAction != null;
        bool anyInquiryActive = TaleWorlds.Library.InformationManager.IsAnyInquiryActive();
        bool decisionInquiryActive = queryDataPresent && anyInquiryActive;
        bool mapActionEnabled = false;
        string mapActionDisabledReason = "not-client";
        if (ModInformation.IsClient)
        {
            mapActionEnabled = CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out TextObject disabledReason);
            mapActionDisabledReason = mapActionEnabled ? "none" : ToToken(disabledReason?.ToString());
        }
        IKingdomDecisionVoteManager inboundVoteManager = null;
        bool localDecisionSuppressionChecked = ModInformation.IsClient && peaceOffer != null &&
                                               KingdomDecisionsVMPatches.TryGetVoteManager(out inboundVoteManager);
        bool localDecisionSuppressed = localDecisionSuppressionChecked &&
                                       inboundVoteManager.ShouldSuppressLocalDecision(peaceOffer);
        bool selectedWarYes = decisionItem?._currentSelectedOption?.Option is
            DeclareWarDecision.DeclareWarDecisionOutcome { ShouldWarBeDeclared: true };
        bool selectedPeaceNo = decisionItem?._currentSelectedOption?.Option is
            MakePeaceKingdomDecision.MakePeaceDecisionOutcome { ShouldPeaceBeDeclared: false };
        bool warDecisionCancelled = warDecision?.ShouldBeCancelled() ?? false;
        bool eligiblePlayerClan = ContainerProvider.TryResolve(out IKingdomDecisionVoteManager voteManager) &&
                                  kingdom.RulingClan != null &&
                                  voteManager.HasEligiblePlayerClan(
                                      new DeclareWarDecision(kingdom.RulingClan, aserai));
        KingdomDiplomacyVM diplomacy = (ScreenManager.TopScreen as GauntletKingdomScreen)?.DataSource?.Diplomacy;
        KingdomDiplomacyProposalActionItemVM warAction = ModInformation.IsClient && diplomacy != null
            ? FindWarProposalAction(diplomacy, aserai)
            : null;
        return $"WAR_PEACE_FIXTURE_STATE role={(ModInformation.IsServer ? "server" : "client")} " +
               $"kingdom={kingdom.StringId} atWar={Bool(AreAtWar(kingdom, aserai))} " +
               $"warDecision={Bool(warDecision != null)} warDecisionCount={warDecisions.Length} " +
               $"warDecisionShouldBeCancelled={Bool(warDecisionCancelled)} " +
               $"eligiblePlayerClan={Bool(eligiblePlayerClan)} " +
               $"inboundPeaceOffer={Bool(peaceOffers.Length != 0)} inboundPeaceOfferCount={peaceOffers.Length} " +
               $"offerShouldBeCancelled={Bool(peaceOffer?.ShouldBeCancelled() ?? false)} " +
               $"offerIsPlayerParticipant={Bool(peaceOffer?.IsPlayerParticipant ?? false)} " +
               $"localDecisionSuppressionChecked={Bool(localDecisionSuppressionChecked)} " +
               $"localDecisionSuppressed={Bool(localDecisionSuppressed)} " +
               $"screenActive={Bool(ScreenManager.TopScreen is GauntletKingdomScreen)} " +
               $"queryDataPresent={Bool(queryDataPresent)} anyInquiryActive={Bool(anyInquiryActive)} " +
               $"decisionInquiryActive={Bool(decisionInquiryActive)} mapActionEnabled={Bool(mapActionEnabled)} " +
               $"mapActionDisabledReason={mapActionDisabledReason} " +
               $"mainHeroPrisoner={Bool(ModInformation.IsClient && Hero.MainHero?.IsPrisoner == true)} " +
               $"mainPartyRaft={Bool(ModInformation.IsClient && MobileParty.MainParty?.IsInRaftState == true)} " +
               $"campaignMissionActive={Bool(ModInformation.IsClient && CampaignMission.Current != null)} " +
               $"playerEncounterActive={Bool(ModInformation.IsClient && PlayerEncounter.Current != null)} " +
               $"playerSiegeActive={Bool(ModInformation.IsClient && PlayerSiege.PlayerSiegeEvent != null)} " +
               $"mainPartyMapEvent={Bool(ModInformation.IsClient && MobileParty.MainParty?.MapEvent != null)} " +
               $"warActionAvailable={Bool(warAction != null)} warActionEnabled={Bool(warAction?.IsEnabled ?? false)} " +
               $"decisionActive={Bool(decisionItem?.IsActive ?? false)} canEnd={Bool(decisionItem?.CanEndDecision ?? false)} " +
               $"finalSelectionDone={Bool(decisionItem?._finalSelectionDone ?? false)} " +
               $"selectedWarYes={Bool(selectedWarYes)} selectedPeaceNo={Bool(selectedPeaceNo)}";
    }

    [CommandLineArgumentFunction("war_peace_fixture_close", "coop.debug.kingdom")]
    public static string CloseScreen(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_close";
        if (!(Game.Current?.GameStateManager?.ActiveState is KingdomState)) return "No active Kingdom screen.";

        Game.Current.GameStateManager.PopState(0);
        return "WAR_PEACE_FIXTURE_SCREEN_CLOSED";
    }

    [CommandLineArgumentFunction("war_peace_fixture_restore", "coop.debug.kingdom")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.war_peace_fixture_restore";
        return RestoreFixture();
    }

    [CommandLineArgumentFunction("war_peace_fixture_restoration_state", "coop.debug.kingdom")]
    public static string RestorationState(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.war_peace_fixture_restoration_state <fixtureKingdomId> <caravanId>";
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 2) return usage;

        Kingdom fixtureKingdom = null;
        bool fixtureKingdomResolved = ContainerProvider.TryResolve(out IObjectManager objectManager) &&
                                      objectManager.TryGetObject(args[0], out fixtureKingdom);
        MobileParty fixtureCaravan = null;
        bool fixtureCaravanResolved = objectManager != null &&
                                      objectManager.TryGetObject(args[1], out fixtureCaravan) &&
                                      fixtureCaravan?.IsActive == true;
        return $"WAR_PEACE_FIXTURE_RESTORATION_STATE fixtureKingdomResolved={Bool(fixtureKingdomResolved)} " +
               $"fixtureKingdomEliminated={Bool(fixtureKingdom?.IsEliminated ?? true)} " +
               $"fixtureCaravanResolved={Bool(fixtureCaravanResolved)} " +
               $"localPlayerKingdom={Clan.PlayerClan?.Kingdom?.StringId ?? "none"} " +
               $"localPlayerMapEvent={Bool(MobileParty.MainParty?.MapEvent != null)}";
    }

    private static string RestoreFixture()
    {
        WarAndPeaceFixture fixture = activeFixture;
        if (fixture == null) return "No war and peace fixture is active.";

        try
        {
            FinalizeAttackMapEvent(fixture);
            RemoveFixtureDecisions(fixture);
            if (AreAtWar(fixture.Kingdom, fixture.Aserai))
            {
                MakePeaceAction.Apply(fixture.Kingdom, fixture.Aserai);
            }
            if (fixture.PlayerClan.Kingdom == fixture.Kingdom)
            {
                fixture.KingdomMembershipState.MoveClanToKingdom(
                    fixture.Kingdom,
                    kingdom: null,
                    clan: fixture.PlayerClan,
                    publishCollectionChanges: true,
                    republishExistingCollections: true);
            }
            if (fixture.PlayerClanWasAtWar)
            {
                DeclareWarAction.ApplyByDefault(fixture.PlayerClan, fixture.Aserai);
            }
            else if (AreAtWar(fixture.PlayerClan, fixture.Aserai))
            {
                MakePeaceAction.Apply(fixture.PlayerClan, fixture.Aserai);
            }

            float influenceDelta = fixture.PlayerInfluence - fixture.PlayerClan.Influence;
            if (Math.Abs(influenceDelta) > 0.001f)
            {
                ChangeClanInfluenceAction.Apply(fixture.PlayerClan, influenceDelta);
            }
            if (fixture.Kingdom.Clans.Count != 0)
            {
                return $"Fixture kingdom still contains {fixture.Kingdom.Clans.Count} clans.";
            }

            bool caravanRestored = !fixture.CaravanCreated || DestroyTemporaryCaravan(fixture.Caravan);
            if (!caravanRestored)
            {
                return "The temporary Aserai caravan was not fully destroyed during fixture restoration.";
            }

            MessageBroker.Instance.Publish(fixture.Kingdom, new DestroyKingdom(fixture.Kingdom));
            if (!fixture.Kingdom.IsEliminated)
            {
                DestroyKingdomAction.Apply(fixture.Kingdom);
            }

            activeFixture = null;
            return $"WAR_PEACE_FIXTURE_RESTORED controller={fixture.ControllerId} kingdom={fixture.KingdomId} " +
                   $"clanDetached={Bool(fixture.PlayerClan.Kingdom == null)} " +
                   $"influenceRestored={Bool(Math.Abs(fixture.PlayerClan.Influence - fixture.PlayerInfluence) <= 0.001f)} " +
                   $"warRestored={Bool(AreAtWar(fixture.PlayerClan, fixture.Aserai) == fixture.PlayerClanWasAtWar)} " +
                   $"mapEventsCleared={Bool(fixture.PlayerParty.MapEvent == null && fixture.Caravan.MapEvent == null)} " +
                   $"partyBehaviorsRestored={Bool(fixture.AttackBehaviorRestored)} " +
                   $"caravanRestored={Bool(caravanRestored)}";
        }
        catch (Exception e)
        {
            return $"War and peace fixture restore failed: {e.GetType().Name}: {e.Message}";
        }
    }

    private static bool TryResolveFixtureInputs(
        string controllerId,
        out Player player,
        out Clan playerClan,
        out MobileParty playerParty,
        out Kingdom aserai,
        out MobileParty caravan,
        out string error)
    {
        player = null;
        playerClan = null;
        playerParty = null;
        aserai = null;
        caravan = null;
        error = null;
        if (!ContainerProvider.TryResolve(out IPlayerManager playerManager) ||
            !ContainerProvider.TryResolve(out IObjectManager objectManager))
        {
            error = "Unable to resolve the player or object manager.";
            return false;
        }
        if (!playerManager.TryGetPlayer(controllerId, out player) || !playerManager.IsConnected(player))
        {
            error = $"Controller {controllerId} must have a connected registered player.";
            return false;
        }
        if (!objectManager.TryGetObject(player.ClanId, out playerClan) ||
            !objectManager.TryGetObject(player.MobilePartyId, out playerParty) ||
            playerParty?.Party == null || !playerParty.IsActive)
        {
            error = "Unable to resolve the connected player's active clan and party.";
            return false;
        }
        if (!TryGetAserai(out aserai))
        {
            error = $"Unable to resolve kingdom {AseraiKingdomId}.";
            return false;
        }

        Kingdom targetKingdom = aserai;
        caravan = MobileParty.All.FirstOrDefault(candidate =>
            candidate?.IsActive == true &&
            candidate.IsCaravan &&
            candidate.Party != null &&
            candidate.MapEvent == null &&
            candidate.CurrentSettlement == null &&
            candidate.MapFaction?.MapFaction == targetKingdom);
        return true;
    }

    private static bool TryGetTemporaryCaravanInputs(
        Kingdom aserai,
        out Settlement settlement,
        out Hero owner,
        out PartyTemplateObject template,
        out string error)
    {
        settlement = Settlement.All.FirstOrDefault(candidate =>
            candidate?.IsTown == true &&
            candidate.OwnerClan?.Kingdom == aserai &&
            candidate.OwnerClan.Leader?.IsAlive == true &&
            candidate.OwnerClan.Leader.HomeSettlement?.OwnerClan != null &&
            candidate.Party?.MapEvent == null &&
            candidate.SiegeEvent == null);
        owner = settlement?.OwnerClan?.Leader;
        template = aserai?.Culture?.CaravanPartyTemplates?.FirstOrDefault(candidate =>
            candidate?.ShipHulls != null && candidate.ShipHulls.Count == 0);
        if (settlement == null || owner == null || template == null)
        {
            error = "No deterministic Aserai town, owner, and land-caravan template are available for the temporary caravan.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryCreateTemporaryAseraiCaravan(
        Kingdom aserai,
        MobileParty playerParty,
        out MobileParty caravan,
        out string error)
    {
        caravan = null;
        if (!TryGetTemporaryCaravanInputs(aserai, out Settlement settlement, out Hero owner,
                out PartyTemplateObject template, out error))
        {
            return false;
        }

        try
        {
            caravan = CaravanPartyComponent.CreateCaravanParty(
                owner,
                settlement,
                template,
                isInitialSpawn: false,
                caravanLeader: null,
                caravanItems: null,
                isElite: false);
            if (caravan?.CurrentSettlement != null)
            {
                LeaveSettlementAction.ApplyForParty(caravan);
            }
            if (caravan == null)
            {
                error = "Vanilla caravan creation returned no party.";
                return false;
            }

            caravan.Position = playerParty.Position;
            caravan.SetMoveModeHold();
            caravan.ResetNavigationToHold();
            caravan.Party.SetVisualAsDirty();
            if (caravan.CurrentSettlement != null || caravan.MapEvent != null ||
                caravan.MapFaction?.MapFaction != aserai ||
                !ContainerProvider.TryResolve(out IObjectManager objectManager) ||
                !objectManager.TryGetId(caravan, out _))
            {
                bool cleanupPassed = DestroyTemporaryCaravan(caravan);
                error = $"The temporary Aserai caravan failed its post-create checks. " +
                        $"Cleanup passed: {Bool(cleanupPassed)}.";
                caravan = null;
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception e)
        {
            bool cleanupPassed = caravan == null || DestroyTemporaryCaravan(caravan);
            caravan = null;
            error = $"Temporary Aserai caravan creation failed: {e.GetType().Name}: {e.Message}. " +
                    $"Cleanup passed: {Bool(cleanupPassed)}.";
            return false;
        }
    }

    private static bool DestroyTemporaryCaravan(MobileParty caravan)
    {
        if (caravan == null) return true;
        if (caravan.MapEvent != null) return false;
        if (caravan.IsActive)
        {
            DestroyPartyAction.Apply(null, caravan);
        }

        bool stillOwned = caravan.CaravanPartyComponent?.Owner?.OwnedCaravans?
            .Contains(caravan.CaravanPartyComponent) == true;
        bool stillRegistered = ContainerProvider.TryResolve(out IObjectManager objectManager) &&
                               objectManager.TryGetId(caravan, out _);
        return !caravan.IsActive && !stillOwned && !stillRegistered;
    }

    private static bool TryGetClientFixtureKingdom(string kingdomId, out Kingdom kingdom, out string error)
    {
        kingdom = null;
        error = null;
        if (!ContainerProvider.TryResolve(out IObjectManager objectManager) ||
            !objectManager.TryGetObject(kingdomId, out kingdom))
        {
            error = $"Unable to resolve fixture kingdom {kingdomId}.";
            return false;
        }
        return true;
    }

    private static bool TryGetAserai(out Kingdom aserai)
    {
        Kingdom target = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == AseraiKingdomId);
        aserai = target;
        return aserai != null;
    }

    private static KingdomDiplomacyProposalActionItemVM FindWarProposalAction(
        KingdomDiplomacyVM diplomacy,
        Kingdom target)
    {
        TextObject support = GameTexts.FindText(
            "str_decision_outcome_support_status",
            KingdomElection.GetElectionOutcomeSupport(new DeclareWarDecision(Clan.PlayerClan, target), Clan.PlayerClan).ToString());
        string explanation = GameTexts.FindText("str_propose_war_explanation")
            .SetTextVariable("SUPPORT", support)
            .ToString();
        return diplomacy.Actions.FirstOrDefault(action => action.Explanation == explanation);
    }

    private static bool CanStartAttack(WarAndPeaceFixture fixture, out string error)
    {
        error = null;
        if (fixture.PlayerParty?.Party == null || fixture.Caravan?.Party == null ||
            !fixture.PlayerParty.IsActive || !fixture.Caravan.IsActive)
        {
            error = "The fixture player party or Aserai caravan is no longer active.";
            return false;
        }
        if (fixture.PlayerParty.MapEvent != null || fixture.Caravan.MapEvent != null)
        {
            error = "The fixture player party and Aserai caravan must both be outside map events.";
            return false;
        }
        if (fixture.PlayerParty.CurrentSettlement != null || fixture.Caravan.CurrentSettlement != null)
        {
            error = "The fixture player party and Aserai caravan must both be outside settlements.";
            return false;
        }
        if (fixture.PlayerParty.Party.MapEvent != null || fixture.Caravan.Party.MapEvent != null)
        {
            error = "The fixture parties must not already belong to a map event.";
            return false;
        }
        return true;
    }

    private static void RemoveFixtureDecisions(WarAndPeaceFixture fixture)
    {
        foreach (KingdomDecision decision in fixture.Kingdom._unresolvedDecisions.ToList())
        {
            fixture.Kingdom.RemoveDecision(decision);
        }
    }

    private static void FinalizeAttackMapEvent(WarAndPeaceFixture fixture)
    {
        MapEvent mapEvent = fixture.AttackMapEvent ?? fixture.PlayerParty?.MapEvent ?? fixture.Caravan?.MapEvent;
        if (mapEvent != null)
        {
            RequestAttackMissionExit(fixture, mapEvent);
            if (!mapEvent.IsFinalized)
            {
                mapEvent.FinalizeEvent();
            }

            PartyBase[] involvedParties = { fixture.PlayerParty?.Party, fixture.Caravan?.Party };
            bool stillAttached = involvedParties.Any(party => party?._mapEventSide?.MapEvent == mapEvent) ||
                                 mapEvent.AttackerSide?.Parties.Count > 0 ||
                                 mapEvent.DefenderSide?.Parties.Count > 0;
            if (stillAttached)
            {
                foreach (PartyBase party in involvedParties)
                {
                    if (party?._mapEventSide?.MapEvent != mapEvent) continue;

                    party._mapEventSide = null;
                    if (party.MobileParty != null)
                    {
                        party.MobileParty.EventPositionAdder = TaleWorlds.Library.Vec2.Zero;
                    }
                    party.SetVisualAsDirty();
                }
                mapEvent.AttackerSide?.Clear();
                mapEvent.DefenderSide?.Clear();
                MessageBroker.Instance.Publish(mapEvent, new MapEventFinalized(mapEvent));
                MessageBroker.Instance.Publish(mapEvent, new InstanceDestroyed<MapEvent>(mapEvent));
            }
        }
        fixture.AttackMapEvent = null;
        if (!RestoreAttackPartyBehaviors(fixture))
        {
            throw new InvalidOperationException("Unable to restore the original player-party and Aserai-caravan behavior.");
        }
    }

    private static bool RestoreAttackPartyBehaviors(WarAndPeaceFixture fixture)
    {
        if (!fixture.AttackBehaviorCaptured) return true;

        fixture.PlayerParty.Position = fixture.PlayerPartyBehavior.PartyPosition;
        fixture.Caravan.Position = fixture.CaravanBehavior.PartyPosition;
        bool restored = fixture.BehaviorSnapshot.TryApply(
                            fixture.PlayerParty,
                            fixture.PlayerPartyBehavior,
                            out _) &&
                        fixture.BehaviorSnapshot.TryApply(
                            fixture.Caravan,
                            fixture.CaravanBehavior,
                            out _);
        if (!restored) return false;

        MessageBroker.Instance.Publish(
            typeof(WarAndPeaceReproductionFixtureCommands),
            new PartyBehaviorChangeAttempted(
                fixture.PlayerParty,
                forcePosition: true,
                isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea));
        MessageBroker.Instance.Publish(
            typeof(WarAndPeaceReproductionFixtureCommands),
            new PartyBehaviorChangeAttempted(
                fixture.Caravan,
                forcePosition: true,
                isCurrentlyAtSea: fixture.Caravan.IsCurrentlyAtSea));
        fixture.AttackBehaviorCaptured = false;
        fixture.AttackBehaviorRestored = true;
        return true;
    }

    private static void RequestAttackMissionExit(WarAndPeaceFixture fixture, MapEvent mapEvent)
    {
        if (!ContainerProvider.TryResolve(out INetwork network) ||
            !ContainerProvider.TryResolve(out IPlayerManager playerManager) ||
            !ContainerProvider.TryResolve(out IMissionMembershipRegistry missionMembership) ||
            !ContainerProvider.TryResolve(out IObjectManager objectManager) ||
            !missionMembership.IsControllerInMission(fixture.ControllerId))
        {
            return;
        }
        if (!playerManager.TryGetPeer(fixture.ControllerId, out var peer) ||
            !objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            throw new InvalidOperationException("Unable to request the fixture client's mission exit.");
        }

        network.Send(peer, new NetworkEndLateJoinModeFixtureMission(mapEventId));
        if (!GameThread.WaitWhilePumping(
                () => !missionMembership.IsControllerInMission(fixture.ControllerId),
                DateTime.UtcNow.AddSeconds(10)))
        {
            throw new InvalidOperationException("The fixture client did not leave the mission before map-event finalization.");
        }
    }

    private static DecisionItemBaseVM GetCurrentDecisionItem()
    {
        return (ScreenManager.TopScreen as GauntletKingdomScreen)?.DataSource?.Decision?.CurrentDecision;
    }

    private static bool AreAtWar(IFaction faction1, IFaction faction2)
    {
        return faction1 != null && faction2 != null && FactionManager.IsAtWarAgainstFaction(faction1, faction2);
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string ToToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";

        return value.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_');
    }

    private sealed class WarAndPeaceFixture
    {
        public string ControllerId { get; }
        public Clan PlayerClan { get; }
        public MobileParty PlayerParty { get; }
        public float PlayerInfluence { get; }
        public bool PlayerClanWasAtWar { get; }
        public Kingdom Kingdom { get; }
        public string KingdomId { get; }
        public Kingdom Aserai { get; }
        public MobileParty Caravan { get; }
        public bool CaravanCreated { get; }
        public IKingdomMembershipState KingdomMembershipState { get; }
        public MapEvent AttackMapEvent { get; set; }
        public IMobilePartyBehaviorSnapshot BehaviorSnapshot { get; set; }
        public PartyBehaviorUpdateData PlayerPartyBehavior { get; set; }
        public PartyBehaviorUpdateData CaravanBehavior { get; set; }
        public bool AttackBehaviorCaptured { get; set; }
        public bool AttackBehaviorRestored { get; set; } = true;

        public WarAndPeaceFixture(
            string controllerId,
            Clan playerClan,
            MobileParty playerParty,
            float playerInfluence,
            bool playerClanWasAtWar,
            Kingdom kingdom,
            string kingdomId,
            Kingdom aserai,
            MobileParty caravan,
            bool caravanCreated,
            IKingdomMembershipState kingdomMembershipState)
        {
            ControllerId = controllerId;
            PlayerClan = playerClan;
            PlayerParty = playerParty;
            PlayerInfluence = playerInfluence;
            PlayerClanWasAtWar = playerClanWasAtWar;
            Kingdom = kingdom;
            KingdomId = kingdomId;
            Aserai = aserai;
            Caravan = caravan;
            CaravanCreated = caravanCreated;
            KingdomMembershipState = kingdomMembershipState;
        }
    }
}
#endif
