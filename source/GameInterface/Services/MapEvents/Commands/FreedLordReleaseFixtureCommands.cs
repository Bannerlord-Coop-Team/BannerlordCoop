#if DEBUG
using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Commands;
using GameInterface.Services.Players;
using GameInterface.Services.Stances.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

internal static class FreedLordReleaseFixtureCommands
{
    private const string DanusticaId = "town_ES1";
    private const string RhagaeaId = "lord_1_14";
    private const string MesuiId = "lord_6_4";
    private const string BagaiId = "lord_6_3";
    private const string FreedReleaseOptionId = "talk_lord_freed_to_lord_release";
    private const string LiberateStartOptionId = "liberate_hero_3";
    private const string LiberateFinishOptionId = "liberate_hero_7";
    private const int PlayerTroops = 80;
    private const int CaptorTroops = 6;

    private static readonly ILogger Logger = LogManager.GetLogger(typeof(FreedLordReleaseFixtureCommands));
    private static readonly Dictionary<string, int> ClientSelections = new Dictionary<string, int>();
    private static FreedLordReleaseFixture fixture;

    [CommandLineArgumentFunction("freed_lord_release_fixture_preflight", "coop.debug.mapevent")]
    public static string PreflightFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.freed_lord_release_fixture_preflight <controllerId>";
        if (fixture != null)
            return "The freed-lord release fixture is already active.";

        if (!TryResolveServices(out var objectManager, out var playerManager, out _, out _, out var error))
            return error;
        if (!TryGetPlayer(playerManager, objectManager, args[0], out var playerHero, out var playerParty, out error))
            return error;
        if (!TryGetHero(objectManager, RhagaeaId, "Rhagaea", out var rhagaea, out error) ||
            !TryGetHero(objectManager, MesuiId, "Mesui", out var mesui, out error) ||
            !TryGetHero(objectManager, BagaiId, "Bagai", out var bagai, out error))
            return error;
        if (!TryValidateFixtureHero(rhagaea, out error) ||
            !TryValidateFixtureHero(mesui, out error) ||
            !TryValidateFixtureHero(bagai, out error))
            return error;

        var parties = new[] { playerParty, rhagaea.PartyBelongedTo, mesui.PartyBelongedTo, bagai.PartyBelongedTo }
            .Where(party => party != null)
            .ToArray();
        if (parties.Distinct().Count() != parties.Length)
            return "Existing fixture parties must be different.";
        if (parties.Any(party => !party.IsActive || party.MapEvent != null || party.Army != null))
            return "Every fixture party must be active and outside armies and map events.";
        if (rhagaea.PartyBelongedTo == null || rhagaea.PartyBelongedTo.LeaderHero != rhagaea ||
            (mesui.PartyBelongedTo != null && mesui.PartyBelongedTo.LeaderHero != mesui) ||
            (bagai.PartyBelongedTo != null && bagai.PartyBelongedTo.LeaderHero != bagai))
            return "Rhagaea must lead her party; Mesui and Bagai must lead any party they belong to.";
        if (Settlement.Find(DanusticaId) == null)
            return "Danustica (town_ES1) was not found.";
        if (playerHero.MapFaction == null || rhagaea.MapFaction == null || playerHero.MapFaction == rhagaea.MapFaction)
            return "The player and Rhagaea must belong to different map factions.";

        return $"Freed-lord release fixture preflight passed: controller={args[0]}, " +
               $"player={playerHero.StringId}|party={playerParty.StringId}, " +
               $"captor={rhagaea.StringId}|party={rhagaea.PartyBelongedTo.StringId}, " +
               $"freedLords={mesui.StringId}|{bagai.StringId}, settlement={DanusticaId}.";
    }

    [CommandLineArgumentFunction("freed_lord_release_fixture_start", "coop.debug.mapevent")]
    public static string StartFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.freed_lord_release_fixture_start <controllerId>";
        if (fixture != null)
            return "The freed-lord release fixture is already active.";

        if (!TryResolveServices(out var objectManager, out var playerManager, out var behaviorSnapshot, out var network, out var error))
            return error;
        if (!TryGetPlayer(playerManager, objectManager, args[0], out var playerHero, out var playerParty, out error))
            return error;
        if (!TryGetHero(objectManager, RhagaeaId, "Rhagaea", out var rhagaea, out error) ||
            !TryGetHero(objectManager, MesuiId, "Mesui", out var mesui, out error) ||
            !TryGetHero(objectManager, BagaiId, "Bagai", out var bagai, out error))
            return error;

        var danustica = Settlement.Find(DanusticaId);
        if (danustica == null)
            return "Danustica (town_ES1) was not found.";
        if (!TryValidateFixtureHero(rhagaea, out error) ||
            !TryValidateFixtureHero(mesui, out error) ||
            !TryValidateFixtureHero(bagai, out error))
            return error;

        var sourceParties = new[]
        {
            playerParty,
            rhagaea.PartyBelongedTo,
            mesui.PartyBelongedTo,
            bagai.PartyBelongedTo,
        }.Where(party => party != null).ToArray();
        if (sourceParties.Distinct().Count() != sourceParties.Length)
            return "Existing fixture parties must be different.";
        if (sourceParties.Any(party => !party.IsActive || party.MapEvent != null || party.Army != null))
            return "Every fixture party must be active and outside armies and map events.";
        if (rhagaea.PartyBelongedTo == null || rhagaea.PartyBelongedTo.LeaderHero != rhagaea ||
            (mesui.PartyBelongedTo != null && mesui.PartyBelongedTo.LeaderHero != mesui) ||
            (bagai.PartyBelongedTo != null && bagai.PartyBelongedTo.LeaderHero != bagai))
            return "Rhagaea must lead her party; Mesui and Bagai must lead any party they belong to.";

        var playerFaction = playerHero.MapFaction;
        var captorFaction = rhagaea.MapFaction;
        if (playerFaction == null || captorFaction == null || playerFaction == captorFaction)
            return "The player and Rhagaea must belong to different map factions.";
        var troop = playerHero.Culture?.BasicTroop ?? rhagaea.Culture?.BasicTroop;
        if (troop == null)
            return "No culture basic troop is available for the fixture.";

        var partySnapshots = sourceParties
            .Select(party => CaptureParty(party, behaviorSnapshot))
            .ToArray();
        var heroSnapshots = partySnapshots
            .SelectMany(snapshot => snapshot.MemberRoster)
            .Where(element => element.Character.IsHero)
            .Select(element => element.Character.HeroObject)
            .Concat(new[] { playerHero, rhagaea, mesui, bagai })
            .Where(hero => hero != null)
            .Distinct()
            .Select(CaptureHero)
            .ToArray();
        var clanSnapshots = heroSnapshots
            .Select(snapshot => snapshot.Hero.Clan)
            .Where(clan => clan != null)
            .Distinct()
            .Select(CaptureClan)
            .ToArray();

        var pendingFixture = new FreedLordReleaseFixture(
            args[0],
            danustica,
            playerHero,
            rhagaea,
            mesui,
            bagai,
            partySnapshots,
            heroSnapshots,
            clanSnapshots,
            CharacterRelationManager.GetHeroRelation(playerHero, mesui),
            CharacterRelationManager.GetHeroRelation(playerHero, bagai),
            AiLordPeaceReleaseFixtureCommands.StanceLinkSnapshot.Capture(playerFaction, captorFaction));
        fixture = pendingFixture;

        try
        {
            if (!pendingFixture.Stance.WasAtWar)
            {
                FactionManager.DeclareWar(playerFaction, captorFaction);
                MessageBroker.Instance.Publish(
                    playerFaction,
                    new FactionWarDeclared(playerFaction, captorFaction, (int)DeclareWarAction.DeclareWarDetail.Default));
            }

            var battlePosition = new CampaignVec2(
                new Vec2(danustica.GatePosition.X - 1.5f, danustica.GatePosition.Y),
                isOnLand: true);
            PrepareParty(playerParty, battlePosition, PlayerTroops, troop, keepLeader: playerHero);

            pendingFixture.CaptorParty = CreateCaptorParty(
                new CampaignVec2(new Vec2(battlePosition.X - 0.4f, battlePosition.Y), isOnLand: true),
                rhagaea);
            MoveHeroToParty(rhagaea, pendingFixture.CaptorParty);
            pendingFixture.CaptorParty.MemberRoster.AddToCounts(troop, CaptorTroops - 1);
            pendingFixture.CaptorParty.SetMoveModeHold();

            // Bagai is added first because vanilla presents the last freed prisoner first.
            TakePrisonerAction.Apply(pendingFixture.CaptorParty.Party, bagai);
            TakePrisonerAction.Apply(pendingFixture.CaptorParty.Party, mesui);

            pendingFixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                pendingFixture.CaptorParty.Party,
                playerParty.Party,
                default);
            if (pendingFixture.MapEvent == null)
                throw new InvalidOperationException("The fixture could not create a field battle.");

            if (!objectManager.TryGetId(pendingFixture.CaptorParty.Party, out string captorPartyId) ||
                !objectManager.TryGetId(playerParty.Party, out string playerPartyId) ||
                !objectManager.TryGetId(pendingFixture.MapEvent, out string mapEventId))
                throw new InvalidOperationException("The fixture could not resolve the encounter network ids.");

            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                $"debug-2572-{Guid.NewGuid():N}",
                captorPartyId,
                playerPartyId,
                mapEventId));

            return FormatServerState("Freed-lord release fixture started", pendingFixture, mapEventId);
        }
        catch (Exception setupException)
        {
            Logger.Error(setupException, "Failed to create freed-lord release fixture");
            try
            {
                RestoreFixture(pendingFixture, behaviorSnapshot);
                fixture = null;
                return $"Fixture setup failed: {setupException.Message}. The baseline was restored.";
            }
            catch (Exception restoreException)
            {
                Logger.Error(restoreException, "Failed to roll back freed-lord release fixture");
                return $"Fixture setup failed: {setupException.Message}. Rollback failed: {restoreException.Message}. Run the restore command.";
            }
        }
    }

    [CommandLineArgumentFunction("freed_lord_release_fixture_state", "coop.debug.mapevent")]
    public static string GetFixtureState(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.freed_lord_release_fixture_state";
        if (fixture == null)
            return "The freed-lord release fixture is not active.";

        string mapEventId = null;
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            objectManager.TryGetId(fixture.MapEvent, out mapEventId);
        return FormatServerState("Freed-lord release fixture state", fixture, mapEventId ?? "unregistered");
    }

    [CommandLineArgumentFunction("freed_lord_release_fixture_route_enemies", "coop.debug.mapevent")]
    public static string RouteEnemies(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.freed_lord_release_fixture_route_enemies";
        if (fixture == null)
            return "The freed-lord release fixture is not active.";
        if (fixture.EnemiesRouted)
            return "The freed-lord release fixture enemies were already routed.";
        if (fixture.MapEvent == null || fixture.MapEvent.IsFinalized)
            return "The freed-lord release fixture battle is no longer active.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !objectManager.TryGetId(fixture.MapEvent, out string mapEventId))
            return "Unable to resolve the fixture battle network state.";

        fixture.EnemiesRouted = true;
        network.SendAll(new NetworkRouteBattleEnemies(mapEventId, enemiesToLeaveFighting: 0));
        return $"Ordered Rhagaea's fixture force to retreat: mapEvent={mapEventId}.";
    }

    [CommandLineArgumentFunction("freed_lord_release_client_reset", "coop.debug.mapevent")]
    public static string ResetClientState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.freed_lord_release_client_reset";

        ClientSelections.Clear();
        return "Freed-lord release client counters reset.";
    }

    [CommandLineArgumentFunction("freed_lord_release_client_state", "coop.debug.mapevent")]
    public static string GetClientState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.freed_lord_release_client_state";

        var encounter = PlayerEncounter.Current;
        var pending = encounter?._capturedAlreadyPrisonerHeroes;
        var conversationManager = Campaign.Current?.ConversationManager;
        var options = conversationManager?.CurOptions;
        var output = new StringBuilder();
        output.AppendLine("PlayerEncounter=" + (encounter == null ? "none" : "active"));
        output.AppendLine("EncounterState=" + (encounter?.EncounterState.ToString() ?? "none"));
        output.AppendLine("ConversationInProgress=" + (conversationManager?.IsConversationInProgress.ToString() ?? "False"));
        output.AppendLine("ConversationHero=" + (Hero.OneToOneConversationHero?.StringId ?? "none"));
        output.AppendLine("ConversationOptions=" + (options == null ? "none" : string.Join(",", options.Select(option => option.Id))));
        output.AppendLine("ReleaseOptionAvailable=" +
                          (options?.Any(option => option.Id == FreedReleaseOptionId || option.Id == LiberateStartOptionId) == true));
        AppendClientHeroState(output, MesuiId, pending);
        AppendClientHeroState(output, BagaiId, pending);
        return output.ToString().TrimEnd();
    }

    [CommandLineArgumentFunction("freed_lord_release_choose", "coop.debug.mapevent")]
    public static string ChooseRelease(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 1 || (args[0] != MesuiId && args[0] != BagaiId))
            return "Usage: coop.debug.mapevent.freed_lord_release_choose <lord_6_4|lord_6_3>";

        var conversationManager = Campaign.Current?.ConversationManager;
        if (conversationManager?.IsConversationInProgress != true)
            return "No freed-lord conversation is active.";
        if (Hero.OneToOneConversationHero?.StringId != args[0])
            return $"The active conversation hero is {Hero.OneToOneConversationHero?.StringId ?? "none"}, not {args[0]}.";
        var optionIds = conversationManager.CurOptions?.Select(option => option.Id).ToArray() ?? Array.Empty<string>();
        string selectedOptions;
        if (optionIds.Contains(FreedReleaseOptionId))
        {
            conversationManager.DoOption(FreedReleaseOptionId);
            selectedOptions = FreedReleaseOptionId;
        }
        else if (optionIds.Contains(LiberateStartOptionId))
        {
            conversationManager.DoOption(LiberateStartOptionId);
            if (conversationManager.CurOptions?.Any(option => option.Id == LiberateFinishOptionId) != true)
                return $"The native {LiberateFinishOptionId} option is not available after {LiberateStartOptionId}.";

            conversationManager.DoOption(LiberateFinishOptionId);
            selectedOptions = LiberateStartOptionId + "," + LiberateFinishOptionId;
        }
        else
        {
            return $"No native freed-lord release option is available; options={string.Join(",", optionIds)}.";
        }

        ClientSelections.TryGetValue(args[0], out int count);
        ClientSelections[args[0]] = count + 1;
        return $"Selected native option(s) {selectedOptions} for {args[0]}; selections={count + 1}.";
    }

    [CommandLineArgumentFunction("freed_lord_release_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.freed_lord_release_fixture_restore";
        if (fixture == null)
            return "The freed-lord release fixture is not active.";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the mobile-party behavior snapshot service.";

        var pendingFixture = fixture;
        try
        {
            RestoreFixture(pendingFixture, behaviorSnapshot);
            var verification = VerifyRestored(pendingFixture);
            if (verification != null)
                return "Fixture restore failed: " + verification;

            fixture = null;
            return "Freed-lord release fixture restored and verified.";
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore freed-lord release fixture");
            return $"Fixture restore failed: {e.Message}. Retry the restore command.";
        }
    }

    internal static bool IsFixtureHero(Hero hero) =>
        fixture?.Heroes.Any(snapshot => snapshot.Hero == hero) == true;

    private static bool TryResolveServices(
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out INetwork network,
        out string error)
    {
        objectManager = null;
        playerManager = null;
        behaviorSnapshot = null;
        network = null;
        error = null;
        if (!ContainerProvider.TryGetContainer(out var container) ||
            !container.TryResolve(out objectManager) ||
            !container.TryResolve(out playerManager) ||
            !container.TryResolve(out behaviorSnapshot) ||
            !container.TryResolve(out network))
        {
            error = "Unable to resolve the fixture services.";
            return false;
        }

        return true;
    }

    private static bool TryGetPlayer(
        IPlayerManager playerManager,
        IObjectManager objectManager,
        string controllerId,
        out Hero hero,
        out MobileParty party,
        out string error)
    {
        hero = null;
        party = null;
        error = null;
        if (!playerManager.TryGetPlayer(controllerId, out var player) || !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }
        if (!objectManager.TryGetObjectWithLogging(player.HeroId, out hero) ||
            !objectManager.TryGetObjectWithLogging(player.MobilePartyId, out party))
        {
            error = $"Unable to resolve player state for {controllerId}.";
            return false;
        }
        if (!party.IsActive || party.MapEvent != null || party.Army != null || party.LeaderHero != hero)
        {
            error = $"Player {controllerId} must lead an active party outside armies and map events.";
            return false;
        }

        return true;
    }

    private static bool TryGetHero(IObjectManager objectManager, string id, string name, out Hero hero, out string error)
    {
        if (!objectManager.TryGetObject(id, out hero))
        {
            error = $"{name} ({id}) was not found.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateFixtureHero(Hero hero, out string error)
    {
        if (!hero.IsAlive || !hero.IsActive || hero.IsPrisoner)
        {
            error = $"{hero.Name} ({hero.StringId}) must be alive, active, and free.";
            return false;
        }

        error = null;
        return true;
    }

    private static PartySnapshot CaptureParty(MobileParty party, IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (!behaviorSnapshot.TryCreate(party, out var behavior))
            throw new InvalidOperationException($"Unable to capture movement state for {party.StringId}.");

        return new PartySnapshot(
            party,
            party.MemberRoster.GetTroopRoster().ToArray(),
            party.PrisonRoster.GetTroopRoster().ToArray(),
            party.ItemRoster.ToArray(),
            party.LeaderHero,
            party.CurrentSettlement,
            party.Position,
            party.IsActive,
            party.RecentEventsMorale,
            party.PartyTradeGold,
            behavior);
    }

    private static HeroSnapshot CaptureHero(Hero hero) =>
        new HeroSnapshot(
            hero,
            hero.HeroState,
            hero.PartyBelongedTo,
            hero.PartyBelongedToAsPrisoner,
            hero.StayingInSettlement,
            hero.CaptivityStartTime,
            hero.HitPoints,
            hero.Gold,
            hero.DeathMark,
            hero.DeathMarkKillerHero,
            Skills.All.ToDictionary(skill => skill, hero.GetSkillValue),
            hero.HeroDeveloper == null
                ? null
                : Skills.All.ToDictionary(skill => skill, hero.HeroDeveloper.GetSkillXp),
            hero.HeroDeveloper?._totalXp ?? 0,
            hero.HeroDeveloper?.UnspentFocusPoints ?? 0,
            hero.HeroDeveloper?.UnspentAttributePoints ?? 0);

    private static ClanSnapshot CaptureClan(Clan clan) =>
        new ClanSnapshot(clan, clan._influence, clan.Renown, clan._tier);

    private static void PrepareParty(
        MobileParty party,
        CampaignVec2 position,
        int totalTroops,
        CharacterObject troop,
        Hero keepLeader)
    {
        if (party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(party);
        ClearRoster(party.MemberRoster);
        party.MemberRoster.AddToCounts(keepLeader.CharacterObject, 1, insertAtFront: true);
        party.MemberRoster.AddToCounts(troop, totalTroops - 1);
        party.ChangePartyLeader(keepLeader);
        keepLeader.HitPoints = keepLeader.MaxHitPoints;
        party.Position = position;
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        PublishForcedPosition(party, resetMovementToHold: true);
    }

    private static MobileParty CreateCaptorParty(CampaignVec2 position, Hero leader)
    {
        var initializationArgs = new CustomPartyComponent.InitializationArgs(
            position,
            0f,
            leader.Clan,
            new TroopRoster(),
            new TroopRoster());
        var component = new CustomPartyComponent(
            null,
            new TextObject("Rhagaea's freed-lord fixture force"),
            leader,
            string.Empty,
            string.Empty,
            0f,
            false,
            initializationArgs,
            leader);
        return MobileParty.CreateParty($"coop_debug_freed_lord_release_{Guid.NewGuid():N}", component);
    }

    private static void MoveHeroToParty(Hero hero, MobileParty targetParty)
    {
        var sourceParty = hero.PartyBelongedTo;
        if (sourceParty.LeaderHero == hero)
            sourceParty.RemovePartyLeader();
        sourceParty.MemberRoster.RemoveTroop(hero.CharacterObject);
        targetParty.MemberRoster.AddToCounts(hero.CharacterObject, 1, insertAtFront: true);
        targetParty.ChangePartyLeader(hero);
    }

    private static void AppendClientHeroState(
        StringBuilder output,
        string heroId,
        IReadOnlyCollection<TroopRosterElement> pending)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(candidate => candidate.StringId == heroId);
        int pendingCount = pending?.Count(element => element.Character?.HeroObject == hero) ?? 0;
        ClientSelections.TryGetValue(heroId, out int selectionCount);
        output.AppendLine($"Hero={heroId}|IsPrisoner={hero?.IsPrisoner.ToString() ?? "missing"}|" +
                          $"CaptorParty={FormatPartyBaseId(hero?.PartyBelongedToAsPrisoner)}|" +
                          $"PendingCount={pendingCount}|Relation={GetRelation(hero)}|Selections={selectionCount}");
    }

    private static string GetRelation(Hero hero) =>
        hero == null || Hero.MainHero == null
            ? "missing"
            : CharacterRelationManager.GetHeroRelation(Hero.MainHero, hero).ToString(CultureInfo.InvariantCulture);

    private static string FormatServerState(string heading, FreedLordReleaseFixture activeFixture, string mapEventId)
    {
        var output = new StringBuilder();
        output.AppendLine(heading);
        output.AppendLine($"Controller={activeFixture.ControllerId}");
        output.AppendLine($"Setting=west of Danustica|Settlement={activeFixture.Settlement.StringId}|{activeFixture.Settlement.Name}");
        output.AppendLine($"MapEvent={mapEventId}|Finalized={activeFixture.MapEvent?.IsFinalized.ToString() ?? "none"}|EnemiesRouted={activeFixture.EnemiesRouted}");
        output.AppendLine($"Captor={activeFixture.Rhagaea.StringId}|Party={activeFixture.CaptorParty?.StringId ?? "none"}|Active={activeFixture.CaptorParty?.IsActive.ToString() ?? "none"}");
        AppendServerHeroState(output, activeFixture.PlayerHero, activeFixture.Mesui, activeFixture.MesuiRelation);
        AppendServerHeroState(output, activeFixture.PlayerHero, activeFixture.Bagai, activeFixture.BagaiRelation);
        output.Append($"AtWar={activeFixture.PlayerHero.MapFaction.IsAtWarWith(activeFixture.Rhagaea.MapFaction)}|OriginallyAtWar={activeFixture.Stance.WasAtWar}");
        return output.ToString();
    }

    private static void AppendServerHeroState(StringBuilder output, Hero playerHero, Hero lord, int baselineRelation)
    {
        int relation = CharacterRelationManager.GetHeroRelation(playerHero, lord);
        output.AppendLine($"FreedLord={lord.StringId}|Name={lord.Name}|IsPrisoner={lord.IsPrisoner}|" +
                          $"CaptorParty={FormatPartyBaseId(lord.PartyBelongedToAsPrisoner)}|" +
                          $"Relation={relation}|BaselineRelation={baselineRelation}|Delta={relation - baselineRelation}");
    }

    private static string FormatPartyBaseId(PartyBase party) =>
        party?.MobileParty?.StringId ?? party?.Settlement?.StringId ?? "none";

    private static void RestoreFixture(
        FreedLordReleaseFixture activeFixture,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (activeFixture.MapEvent != null && !activeFixture.MapEvent.IsFinalized)
            activeFixture.MapEvent.FinalizeEvent();

        foreach (var hero in activeFixture.Heroes)
        {
            if (hero.Hero.IsPrisoner)
                EndCaptivityAction.ApplyByPeace(hero.Hero);
        }

        if (activeFixture.CaptorParty?.IsActive == true && activeFixture.CaptorParty.MapEvent == null)
            DestroyPartyAction.Apply(null, activeFixture.CaptorParty);

        foreach (var hero in activeFixture.Heroes)
            RestoreHeroState(hero);
        foreach (var party in activeFixture.Parties)
            RestoreParty(party, behaviorSnapshot);
        foreach (var hero in activeFixture.Heroes)
            RestoreHeroMembership(hero);
        foreach (var clan in activeFixture.Clans)
            RestoreClan(clan);

        CharacterRelationManager.SetHeroRelation(activeFixture.PlayerHero, activeFixture.Mesui, activeFixture.MesuiRelation);
        CharacterRelationManager.SetHeroRelation(activeFixture.PlayerHero, activeFixture.Bagai, activeFixture.BagaiRelation);
        activeFixture.Stance.Restore(true);
    }

    private static void RestoreHeroState(HeroSnapshot snapshot)
    {
        snapshot.Hero.StayingInSettlement = snapshot.StayingInSettlement;
        snapshot.Hero.CaptivityStartTime = snapshot.CaptivityStartTime;
        snapshot.Hero.DeathMark = snapshot.DeathMark;
        snapshot.Hero.DeathMarkKillerHero = snapshot.DeathMarkKillerHero;
        snapshot.Hero.HitPoints = snapshot.HitPoints;
        snapshot.Hero.Gold = snapshot.Gold;
        snapshot.Hero.ChangeState(snapshot.State);

        foreach (var skill in snapshot.SkillLevels)
            snapshot.Hero.SetSkillValue(skill.Key, skill.Value);
        if (snapshot.Hero.HeroDeveloper == null || snapshot.SkillXps == null)
            return;

        foreach (var skillXp in snapshot.SkillXps)
            snapshot.Hero.HeroDeveloper.SetSkillXp(skillXp.Key, skillXp.Value);
        snapshot.Hero.HeroDeveloper._totalXp = snapshot.TotalXp;
        snapshot.Hero.HeroDeveloper.UnspentFocusPoints = snapshot.UnspentFocusPoints;
        snapshot.Hero.HeroDeveloper.UnspentAttributePoints = snapshot.UnspentAttributePoints;
    }

    private static void RestoreClan(ClanSnapshot snapshot)
    {
        snapshot.Clan._influence = snapshot.Influence;
        snapshot.Clan.Renown = snapshot.Renown;
        MessageBroker.Instance.Publish(
            snapshot.Clan,
            new ClanRenownChanged(snapshot.Clan.StringId, snapshot.Renown));
        snapshot.Clan._tier = snapshot.Tier;
    }

    private static void RestoreParty(PartySnapshot snapshot, IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (snapshot.Party.CurrentSettlement != null && snapshot.Party.CurrentSettlement != snapshot.Settlement)
            LeaveSettlementAction.ApplyForParty(snapshot.Party);
        snapshot.Party.IsActive = snapshot.WasActive;
        RestoreRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.Items)
            snapshot.Party.ItemRoster.Add(element);
        snapshot.Party.RecentEventsMorale = snapshot.RecentEventsMorale;
        snapshot.Party.PartyTradeGold = snapshot.PartyTradeGold;
        snapshot.Party.Position = snapshot.Position;
        snapshot.Party.ChangePartyLeader(snapshot.LeaderHero);
        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            throw new InvalidOperationException($"Unable to restore movement state for {snapshot.Party.StringId}.");
        if (snapshot.Settlement != null && snapshot.Party.CurrentSettlement != snapshot.Settlement)
            EnterSettlementAction.ApplyForParty(snapshot.Party, snapshot.Settlement);
        PublishForcedPosition(snapshot.Party, resetMovementToHold: false);
    }

    private static void RestoreHeroMembership(HeroSnapshot snapshot)
    {
        if (snapshot.Hero.PartyBelongedToAsPrisoner != snapshot.PrisonerParty)
        {
            if (snapshot.Hero.PartyBelongedToAsPrisoner != null)
                snapshot.Hero.OnRemovedFromPartyAsPrisoner(snapshot.Hero.PartyBelongedToAsPrisoner);
            if (snapshot.PrisonerParty != null)
                snapshot.Hero.OnAddedToPartyAsPrisoner(snapshot.PrisonerParty);
        }
        if (snapshot.Hero.PartyBelongedTo != snapshot.Party)
        {
            if (snapshot.Hero.PartyBelongedTo != null)
                snapshot.Hero.OnRemovedFromParty(snapshot.Hero.PartyBelongedTo);
            if (snapshot.Party != null)
                snapshot.Hero.OnAddedToParty(snapshot.Party);
        }
    }

    private static void RestoreRoster(TroopRoster roster, TroopRosterElement[] baseline)
    {
        ClearRoster(roster);
        foreach (var element in baseline)
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
    }

    private static void ClearRoster(TroopRoster roster)
    {
        for (int index = roster.Count - 1; index >= 0; index--)
        {
            var element = roster.GetElementCopyAtIndex(index);
            roster.AddToCountsAtIndex(index, -element.Number, -element.WoundedNumber, 0, false);
        }
        roster.RemoveZeroCounts();
    }

    private static void PublishForcedPosition(MobileParty party, bool resetMovementToHold)
    {
        MessageBroker.Instance.Publish(
            typeof(FreedLordReleaseFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea,
                resetMovementToHold: resetMovementToHold));
    }

    private static string VerifyRestored(FreedLordReleaseFixture restoredFixture)
    {
        foreach (var party in restoredFixture.Parties)
        {
            if (!RosterMatches(party.Party.MemberRoster, party.MemberRoster))
                return $"member roster differs for {party.Party.StringId}.";
            if (!RosterMatches(party.Party.PrisonRoster, party.PrisonRoster))
                return $"prison roster differs for {party.Party.StringId}.";
            if (party.Party.LeaderHero != party.LeaderHero)
                return $"leader differs for {party.Party.StringId}.";
            if (party.Party.CurrentSettlement != party.Settlement)
                return $"settlement differs for {party.Party.StringId}.";
            if (party.Party.IsActive != party.WasActive)
                return $"activity differs for {party.Party.StringId}.";
            if (party.Settlement == null && !party.Party.Position.Equals(party.Position))
                return $"position differs for {party.Party.StringId}.";
            if (!ItemsMatch(party.Party.ItemRoster, party.Items))
                return $"item roster differs for {party.Party.StringId}.";
            if (party.Party.RecentEventsMorale != party.RecentEventsMorale ||
                party.Party.PartyTradeGold != party.PartyTradeGold)
                return $"morale or trade gold differs for {party.Party.StringId}.";
        }
        foreach (var hero in restoredFixture.Heroes)
        {
            if (hero.Hero.HeroState != hero.State ||
                hero.Hero.PartyBelongedTo != hero.Party ||
                hero.Hero.PartyBelongedToAsPrisoner != hero.PrisonerParty)
                return $"membership or state differs for {hero.Hero.StringId}.";
            if (hero.Hero.StayingInSettlement != hero.StayingInSettlement ||
                hero.Hero.CaptivityStartTime != hero.CaptivityStartTime ||
                hero.Hero.HitPoints != hero.HitPoints ||
                hero.Hero.Gold != hero.Gold ||
                hero.Hero.DeathMark != hero.DeathMark ||
                hero.Hero.DeathMarkKillerHero != hero.DeathMarkKillerHero)
                return $"hero state differs for {hero.Hero.StringId}.";
            if (hero.SkillLevels.Any(skill => hero.Hero.GetSkillValue(skill.Key) != skill.Value))
                return $"skill levels differ for {hero.Hero.StringId}.";
            if (hero.Hero.HeroDeveloper != null && hero.SkillXps != null &&
                (hero.SkillXps.Any(skill => hero.Hero.HeroDeveloper.GetSkillXp(skill.Key) != skill.Value) ||
                 hero.Hero.HeroDeveloper._totalXp != hero.TotalXp ||
                 hero.Hero.HeroDeveloper.UnspentFocusPoints != hero.UnspentFocusPoints ||
                 hero.Hero.HeroDeveloper.UnspentAttributePoints != hero.UnspentAttributePoints))
                return $"skill experience differs for {hero.Hero.StringId}.";
        }
        foreach (var clan in restoredFixture.Clans)
        {
            if (clan.Clan._influence != clan.Influence ||
                clan.Clan.Renown != clan.Renown ||
                clan.Clan._tier != clan.Tier)
                return $"progression differs for clan {clan.Clan.StringId}.";
        }
        if (CharacterRelationManager.GetHeroRelation(restoredFixture.PlayerHero, restoredFixture.Mesui) != restoredFixture.MesuiRelation)
            return "Mesui relation differs from the baseline.";
        if (CharacterRelationManager.GetHeroRelation(restoredFixture.PlayerHero, restoredFixture.Bagai) != restoredFixture.BagaiRelation)
            return "Bagai relation differs from the baseline.";
        return restoredFixture.Stance.VerifyRestored();
    }

    private static bool RosterMatches(TroopRoster roster, IReadOnlyCollection<TroopRosterElement> baseline)
    {
        if (roster.Count != baseline.Count) return false;
        foreach (var expected in baseline)
        {
            int index = roster.FindIndexOfTroop(expected.Character);
            if (index < 0) return false;
            var actual = roster.GetElementCopyAtIndex(index);
            if (actual.Number != expected.Number || actual.WoundedNumber != expected.WoundedNumber || actual.Xp != expected.Xp)
                return false;
        }
        return true;
    }

    private static bool ItemsMatch(ItemRoster roster, IReadOnlyCollection<ItemRosterElement> baseline)
    {
        if (roster.Count != baseline.Count) return false;
        foreach (var expected in baseline)
        {
            int index = roster.FindIndexOfElement(expected.EquipmentElement);
            if (index < 0 || roster[index].Amount != expected.Amount)
                return false;
        }
        return true;
    }

    private sealed class FreedLordReleaseFixture
    {
        public string ControllerId { get; }
        public Settlement Settlement { get; }
        public Hero PlayerHero { get; }
        public Hero Rhagaea { get; }
        public Hero Mesui { get; }
        public Hero Bagai { get; }
        public PartySnapshot[] Parties { get; }
        public HeroSnapshot[] Heroes { get; }
        public ClanSnapshot[] Clans { get; }
        public int MesuiRelation { get; }
        public int BagaiRelation { get; }
        public AiLordPeaceReleaseFixtureCommands.StanceLinkSnapshot Stance { get; }
        public MobileParty CaptorParty { get; set; }
        public MapEvent MapEvent { get; set; }
        public bool EnemiesRouted { get; set; }

        public FreedLordReleaseFixture(
            string controllerId,
            Settlement settlement,
            Hero playerHero,
            Hero rhagaea,
            Hero mesui,
            Hero bagai,
            PartySnapshot[] parties,
            HeroSnapshot[] heroes,
            ClanSnapshot[] clans,
            int mesuiRelation,
            int bagaiRelation,
            AiLordPeaceReleaseFixtureCommands.StanceLinkSnapshot stance)
        {
            ControllerId = controllerId;
            Settlement = settlement;
            PlayerHero = playerHero;
            Rhagaea = rhagaea;
            Mesui = mesui;
            Bagai = bagai;
            Parties = parties;
            Heroes = heroes;
            Clans = clans;
            MesuiRelation = mesuiRelation;
            BagaiRelation = bagaiRelation;
            Stance = stance;
        }
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party { get; }
        public TroopRosterElement[] MemberRoster { get; }
        public TroopRosterElement[] PrisonRoster { get; }
        public ItemRosterElement[] Items { get; }
        public Hero LeaderHero { get; }
        public Settlement Settlement { get; }
        public CampaignVec2 Position { get; }
        public bool WasActive { get; }
        public float RecentEventsMorale { get; }
        public int PartyTradeGold { get; }
        public PartyBehaviorUpdateData Behavior { get; }

        public PartySnapshot(
            MobileParty party,
            TroopRosterElement[] memberRoster,
            TroopRosterElement[] prisonRoster,
            ItemRosterElement[] items,
            Hero leaderHero,
            Settlement settlement,
            CampaignVec2 position,
            bool wasActive,
            float recentEventsMorale,
            int partyTradeGold,
            PartyBehaviorUpdateData behavior)
        {
            Party = party;
            MemberRoster = memberRoster;
            PrisonRoster = prisonRoster;
            Items = items;
            LeaderHero = leaderHero;
            Settlement = settlement;
            Position = position;
            WasActive = wasActive;
            RecentEventsMorale = recentEventsMorale;
            PartyTradeGold = partyTradeGold;
            Behavior = behavior;
        }
    }

    private sealed class HeroSnapshot
    {
        public Hero Hero { get; }
        public Hero.CharacterStates State { get; }
        public MobileParty Party { get; }
        public PartyBase PrisonerParty { get; }
        public Settlement StayingInSettlement { get; }
        public CampaignTime CaptivityStartTime { get; }
        public int HitPoints { get; }
        public int Gold { get; }
        public KillCharacterAction.KillCharacterActionDetail DeathMark { get; }
        public Hero DeathMarkKillerHero { get; }
        public Dictionary<SkillObject, int> SkillLevels { get; }
        public Dictionary<SkillObject, float> SkillXps { get; }
        public int TotalXp { get; }
        public int UnspentFocusPoints { get; }
        public int UnspentAttributePoints { get; }

        public HeroSnapshot(
            Hero hero,
            Hero.CharacterStates state,
            MobileParty party,
            PartyBase prisonerParty,
            Settlement stayingInSettlement,
            CampaignTime captivityStartTime,
            int hitPoints,
            int gold,
            KillCharacterAction.KillCharacterActionDetail deathMark,
            Hero deathMarkKillerHero,
            Dictionary<SkillObject, int> skillLevels,
            Dictionary<SkillObject, float> skillXps,
            int totalXp,
            int unspentFocusPoints,
            int unspentAttributePoints)
        {
            Hero = hero;
            State = state;
            Party = party;
            PrisonerParty = prisonerParty;
            StayingInSettlement = stayingInSettlement;
            CaptivityStartTime = captivityStartTime;
            HitPoints = hitPoints;
            Gold = gold;
            DeathMark = deathMark;
            DeathMarkKillerHero = deathMarkKillerHero;
            SkillLevels = skillLevels;
            SkillXps = skillXps;
            TotalXp = totalXp;
            UnspentFocusPoints = unspentFocusPoints;
            UnspentAttributePoints = unspentAttributePoints;
        }
    }

    private sealed class ClanSnapshot
    {
        public Clan Clan { get; }
        public float Influence { get; }
        public float Renown { get; }
        public int Tier { get; }

        public ClanSnapshot(Clan clan, float influence, float renown, int tier)
        {
            Clan = clan;
            Influence = influence;
            Renown = renown;
            Tier = tier;
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(Hero), nameof(Hero.CanDie))]
internal static class FreedLordReleaseFixtureHeroDeathPatch
{
    [HarmonyLib.HarmonyPrefix]
    private static bool Prefix(
        Hero __instance,
        KillCharacterAction.KillCharacterActionDetail causeOfDeath,
        ref bool __result)
    {
        if (causeOfDeath != KillCharacterAction.KillCharacterActionDetail.DiedInBattle ||
            !FreedLordReleaseFixtureCommands.IsFixtureHero(__instance))
            return true;

        __result = false;
        return false;
    }
}
#endif
