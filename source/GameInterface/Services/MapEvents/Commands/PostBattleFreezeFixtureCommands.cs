#if DEBUG
using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Auto;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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

internal class PostBattleFreezeFixtureCommands
{
    private const string TubilisCastleId = "castle_A1";
    private const int FirstPlayerTroops = 179;
    private const int SecondPlayerTroops = 202;
    private const int AiLordTroops = 137;

    private static PostBattleFreezeFixture fixture;

    [CommandLineArgumentFunction("post_battle_freeze_fixture_start", "coop.debug.mapevent")]
    public static string StartFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.post_battle_freeze_fixture_start <firstControllerId> <secondControllerId>";
        if (fixture != null)
            return "The post-battle freeze fixture is already active.";

        if (!TryResolveServices(
                out var objectManager,
                out var playerManager,
                out var behaviorSnapshot,
                out var timeControl,
                out var error))
            return error;

        if (!TryGetReadyPlayerParty(playerManager, objectManager, args[0], out var firstPlayer, out error) ||
            !TryGetReadyPlayerParty(playerManager, objectManager, args[1], out var secondPlayer, out error))
            return error;
        if (firstPlayer == secondPlayer)
            return "The fixture requires two different player parties.";

        var tubilisCastle = Settlement.Find(TubilisCastleId);
        if (tubilisCastle == null)
            return $"Settlement {TubilisCastleId} was not found.";

        var aiLordParty = MobileParty.All
            .Where(p => p.IsActive &&
                        p != firstPlayer &&
                        p != secondPlayer &&
                        p.LeaderHero?.IsLord == true &&
                        !p.IsPlayerParty() &&
                        p.MapEvent == null &&
                        p.CurrentSettlement == null &&
                        p.MemberRoster.TotalManCount > 0 &&
                        p.MapFaction != null &&
                        firstPlayer.MapFaction != null &&
                        FactionManager.IsAtWarAgainstFaction(p.MapFaction, firstPlayer.MapFaction))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(tubilisCastle.Position.ToVec2()))
            .FirstOrDefault();
        if (aiLordParty == null)
            return "No active AI lord party at war with the first player is available.";

        var troop = FindFixtureTroop(firstPlayer, secondPlayer, aiLordParty);
        if (troop == null)
            return "No regular troop template is available for the fixture.";

        var firstPlayerSnapshot = CaptureParty(firstPlayer, behaviorSnapshot);
        var secondPlayerSnapshot = CaptureParty(secondPlayer, behaviorSnapshot);
        var aiLordSnapshot = CaptureParty(aiLordParty, behaviorSnapshot);
        var partySnapshots = new[]
        {
            firstPlayerSnapshot,
            secondPlayerSnapshot,
            aiLordSnapshot,
        };
        var heroSnapshots = partySnapshots
            .SelectMany(party => party.MemberRoster.Concat(party.PrisonRoster))
            .Where(element => element.Character.IsHero)
            .Select(element => element.Character.HeroObject)
            .Concat(partySnapshots.Select(party => party.LeaderHero))
            .Where(hero => hero != null)
            .Distinct()
            .Select(CaptureHero)
            .ToArray();
        var battleHeroes = firstPlayerSnapshot.MemberRoster
            .Concat(secondPlayerSnapshot.MemberRoster)
            .Where(element => element.Character.IsHero)
            .Select(element => element.Character.HeroObject)
            .Concat(new[]
            {
                firstPlayerSnapshot.LeaderHero,
                secondPlayerSnapshot.LeaderHero,
                aiLordParty.LeaderHero,
            })
            .Where(hero => hero != null)
            .Distinct()
            .ToArray();
        var clanSnapshots = heroSnapshots
            .Select(snapshot => snapshot.Hero.Clan)
            .Where(clan => clan != null)
            .Distinct()
            .Select(CaptureClan)
            .ToArray();

        var pendingFixture = new PostBattleFreezeFixture(
            args[0],
            args[1],
            tubilisCastle,
            firstPlayerSnapshot,
            secondPlayerSnapshot,
            aiLordSnapshot,
            aiLordParty.LeaderHero,
            battleHeroes,
            heroSnapshots,
            clanSnapshots,
            timeControl.GetTimeControl());

        fixture = pendingFixture;
        try
        {
            var battlePosition = new CampaignVec2(
                new Vec2(tubilisCastle.Position.X, tubilisCastle.Position.Y - 1.5f),
                true);

            pendingFixture.AiParty = CreateDisposableAiLordParty(
                battlePosition,
                pendingFixture.AiLeader,
                aiLordParty.ActualClan);

            aiLordParty.RemovePartyLeader();
            aiLordParty.MemberRoster.AddToCounts(pendingFixture.AiLeader.CharacterObject, -1);
            pendingFixture.AiParty.MemberRoster.AddToCounts(pendingFixture.AiLeader.CharacterObject, 1);

            PrepareParty(firstPlayer, battlePosition, FirstPlayerTroops, troop);
            PrepareParty(secondPlayer, battlePosition, SecondPlayerTroops, troop);
            PrepareParty(pendingFixture.AiParty, battlePosition, AiLordTroops, troop);

            pendingFixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                pendingFixture.AiParty.Party,
                firstPlayer.Party,
                default);
            if (pendingFixture.MapEvent == null)
                throw new InvalidOperationException("The fixture could not create a field battle.");

            secondPlayer.Party.MapEventSide = firstPlayer.Party.MapEventSide;

            return FormatState("Post-battle freeze fixture started", pendingFixture, timeControl);
        }
        catch (Exception setupException)
        {
            try
            {
                RestoreFixture(pendingFixture, behaviorSnapshot, timeControl);
                fixture = null;
            }
            catch (Exception restoreException)
            {
                return $"Fixture setup failed: {setupException.Message}. " +
                       $"Rollback failed: {restoreException.Message}. Run the restore command.";
            }

            return $"Fixture setup failed: {setupException.Message}. The baseline was restored.";
        }
    }

    [CommandLineArgumentFunction("post_battle_freeze_fixture_open", "coop.debug.mapevent")]
    public static string OpenFixtureEncounters(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.post_battle_freeze_fixture_open";
        if (fixture == null)
            return "The post-battle freeze fixture is not active.";
        if (fixture.EncountersOpened)
            return "The post-battle freeze fixture encounters are already open.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve the encounter services.";

        if (!objectManager.TryGetId(fixture.AiParty.Party, out string aiPartyId) ||
            !objectManager.TryGetId(fixture.FirstPlayer.Party.Party, out string firstPlayerPartyId) ||
            !objectManager.TryGetId(fixture.SecondPlayer.Party.Party, out string secondPlayerPartyId) ||
            !objectManager.TryGetId(fixture.MapEvent, out string mapEventId))
            return "The fixture could not resolve the battle's network ids.";

        network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
            $"debug-2218-first-{Guid.NewGuid():N}",
            aiPartyId,
            firstPlayerPartyId,
            mapEventId));
        network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
            $"debug-2218-second-{Guid.NewGuid():N}",
            aiPartyId,
            secondPlayerPartyId,
            mapEventId));
        fixture.EncountersOpened = true;
        return $"Opened the post-battle freeze fixture encounter for " +
               $"{fixture.FirstControllerId} and {fixture.SecondControllerId}.";
    }

    [CommandLineArgumentFunction("post_battle_freeze_fixture_state", "coop.debug.mapevent")]
    public static string GetFixtureState(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server. Use the existing client observation commands on each client.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.post_battle_freeze_fixture_state";
        if (fixture == null)
            return "The post-battle freeze fixture is not active.";
        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            return "Unable to resolve time control.";

        return FormatState("Post-battle freeze fixture state", fixture, timeControl);
    }

    [CommandLineArgumentFunction("post_battle_freeze_fixture_unpause", "coop.debug.mapevent")]
    public static string UnpauseFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.post_battle_freeze_fixture_unpause";
        if (fixture == null)
            return "The post-battle freeze fixture is not active.";
        if (fixture.FirstPlayer.Party.MapEvent != null || fixture.SecondPlayer.Party.MapEvent != null)
            return "Both player parties must leave the battle before the unpause probe.";
        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            return "Unable to resolve time control.";

        fixture.ProbeStartedAt = CampaignTime.Now;
        timeControl.ServerSetTimeControl(TimeControlEnum.Play_1x);
        return $"Post-battle unpause requested at {fixture.ProbeStartedAt.NumTicks} ticks. " +
               "Submit coop.debug.mobileparty.move_offset 0.5 0 on both clients, then check fixture state.";
    }

    [CommandLineArgumentFunction("post_battle_freeze_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.post_battle_freeze_fixture_restore";
        if (fixture == null)
            return "The post-battle freeze fixture is not active.";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            return "Unable to resolve fixture restore services.";

        try
        {
            var pendingFixture = fixture;
            RestoreFixture(pendingFixture, behaviorSnapshot, timeControl);
            fixture = null;
            return "Post-battle freeze fixture restored.";
        }
        catch (Exception e)
        {
            return $"Fixture restore failed: {e.Message}. Retry the restore command.";
        }
    }

    private static bool TryResolveServices(
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out ITimeControlInterface timeControl,
        out string error)
    {
        objectManager = null;
        playerManager = null;
        behaviorSnapshot = null;
        timeControl = null;
        error = null;

        if (!ContainerProvider.TryGetContainer(out var container) ||
            !container.TryResolve(out objectManager) ||
            !container.TryResolve(out playerManager) ||
            !container.TryResolve(out behaviorSnapshot) ||
            !container.TryResolve(out timeControl))
        {
            error = "Unable to resolve the fixture services.";
            return false;
        }

        return true;
    }

    private static bool TryGetReadyPlayerParty(
        IPlayerManager playerManager,
        IObjectManager objectManager,
        string controllerId,
        out MobileParty party,
        out string error)
    {
        party = null;
        error = null;

        if (!playerManager.TryGetPlayer(controllerId, out var player) || !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }
        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out party))
        {
            error = $"Unable to resolve player party for {controllerId}.";
            return false;
        }
        if (!party.IsActive || party.MapEvent != null || party.CurrentSettlement != null)
        {
            error = $"Player {controllerId} must have an active party outside settlements and map events.";
            return false;
        }

        return true;
    }

    private static CharacterObject FindFixtureTroop(params MobileParty[] parties)
    {
        foreach (var party in parties)
        {
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                if (!element.Character.IsHero)
                    return element.Character;
            }
        }

        return CharacterObject.All.FirstOrDefault(character => !character.IsHero);
    }

    private static MobileParty CreateDisposableAiLordParty(
        CampaignVec2 position,
        Hero leader,
        Clan clan)
    {
        var initializationArgs = new CustomPartyComponent.InitializationArgs(
            position,
            0f,
            clan,
            new TroopRoster(),
            new TroopRoster());
        var component = new CustomPartyComponent(
            null,
            new TextObject("Post-battle freeze fixture"),
            leader,
            string.Empty,
            string.Empty,
            0f,
            false,
            initializationArgs,
            leader);

        return MobileParty.CreateParty($"coop_debug_post_battle_freeze_{Guid.NewGuid():N}", component);
    }

    private static PartySnapshot CaptureParty(
        MobileParty party,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (!behaviorSnapshot.TryCreate(party, out var behavior))
            throw new InvalidOperationException($"Unable to capture movement state for {party.StringId}.");

        return new PartySnapshot(
            party,
            party.MemberRoster.GetTroopRoster().ToArray(),
            party.PrisonRoster.GetTroopRoster().ToArray(),
            party.ItemRoster.ToArray(),
            party.LeaderHero,
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
        new ClanSnapshot(
            clan,
            clan._influence,
            clan.Renown,
            clan._tier);

    private static void PrepareParty(
        MobileParty party,
        CampaignVec2 position,
        int totalTroops,
        CharacterObject troop)
    {
        ClearRoster(party.MemberRoster, keepHeroes: true);
        int regularTroops = totalTroops - party.MemberRoster.TotalManCount;
        if (regularTroops < 1)
            throw new InvalidOperationException($"Party {party.StringId} has too many heroes for the fixture.");

        party.MemberRoster.AddToCounts(troop, regularTroops);
        party.Position = position;
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(PostBattleFreezeFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: true));
    }

    private static void RestoreFixture(
        PostBattleFreezeFixture activeFixture,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ITimeControlInterface timeControl)
    {
        if (activeFixture.MapEvent != null && !activeFixture.MapEvent.IsFinalized)
            activeFixture.MapEvent.FinalizeEvent();

        if (activeFixture.AiParty?.IsActive == true)
            DestroyPartyAction.Apply(null, activeFixture.AiParty);

        foreach (var hero in activeFixture.Heroes)
            RestoreHeroProgression(hero);

        RestoreParty(activeFixture.FirstPlayer, behaviorSnapshot);
        RestoreParty(activeFixture.SecondPlayer, behaviorSnapshot);
        RestoreParty(activeFixture.AiLord, behaviorSnapshot);

        foreach (var hero in activeFixture.Heroes)
            RestoreHeroMembership(hero);
        foreach (var clan in activeFixture.Clans)
            RestoreClan(clan);

        timeControl.ServerSetTimeControl(activeFixture.OriginalTimeControl);
    }

    internal static bool IsFixtureHero(Hero hero) =>
        fixture?.BattleHeroes.Contains(hero) == true;

    private static void RestoreHeroProgression(HeroSnapshot snapshot)
    {
        if (snapshot.Hero.IsPrisoner)
            EndCaptivityAction.ApplyByPeace(snapshot.Hero);

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

    private static void RestoreClan(ClanSnapshot snapshot)
    {
        snapshot.Clan._influence = snapshot.Influence;
        snapshot.Clan.Renown = snapshot.Renown;
        snapshot.Clan._tier = snapshot.Tier;
    }

    private static void RestoreParty(
        PartySnapshot snapshot,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        snapshot.Party.IsActive = snapshot.WasActive;
        RestoreRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        RestoreItems(snapshot.Party.ItemRoster, snapshot.Items);
        snapshot.Party.RecentEventsMorale = snapshot.RecentEventsMorale;
        snapshot.Party.PartyTradeGold = snapshot.PartyTradeGold;
        snapshot.Party.Position = snapshot.Position;
        snapshot.Party.ChangePartyLeader(snapshot.LeaderHero);

        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            throw new InvalidOperationException($"Unable to restore movement state for {snapshot.Party.StringId}.");

        MessageBroker.Instance.Publish(
            typeof(PostBattleFreezeFixtureCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: snapshot.Behavior.IsCurrentlyAtSea));
    }

    private static void RestoreItems(ItemRoster roster, ItemRosterElement[] baseline)
    {
        roster.Clear();
        foreach (var element in baseline)
            roster.AddToCounts(element.EquipmentElement, element.Amount);
    }

    private static void RestoreRoster(TroopRoster roster, TroopRosterElement[] baseline)
    {
        ClearRoster(roster, keepHeroes: false);
        foreach (var element in baseline)
        {
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }
    }

    private static void ClearRoster(TroopRoster roster, bool keepHeroes)
    {
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (keepHeroes && element.Character.IsHero) continue;

            roster.AddToCountsAtIndex(
                i,
                -element.Number,
                -element.WoundedNumber,
                0,
                false);
        }
        roster.RemoveZeroCounts();
    }

    private static string FormatState(
        string heading,
        PostBattleFreezeFixture activeFixture,
        ITimeControlInterface timeControl)
    {
        var output = new StringBuilder();
        output.AppendLine(heading);
        output.AppendLine($"Settlement={activeFixture.Settlement.StringId}|{activeFixture.Settlement.Name}");
        output.AppendLine($"MapEvent={activeFixture.MapEvent?.StringId ?? "none"}");
        output.AppendLine($"EncountersOpened={activeFixture.EncountersOpened}");
        output.AppendLine($"BattleState={activeFixture.MapEvent?.BattleState.ToString() ?? "none"}");
        output.AppendLine($"ResultsApplied={activeFixture.MapEvent?._mapEventResultsApplied.ToString() ?? "none"}");
        AppendPartyState(output, "FirstPlayer", activeFixture.FirstControllerId, activeFixture.FirstPlayer.Party);
        AppendPartyState(output, "SecondPlayer", activeFixture.SecondControllerId, activeFixture.SecondPlayer.Party);
        AppendPartyState(output, "AiLord", activeFixture.AiLeader.StringId, activeFixture.AiParty);
        output.AppendLine($"TimeMode={timeControl.GetTimeControl()}");
        output.AppendLine($"CampaignTicks={CampaignTime.Now.NumTicks}");
        output.Append($"ProbeStartedTicks={(activeFixture.ProbeStartedAt == CampaignTime.Zero ? "none" : activeFixture.ProbeStartedAt.NumTicks.ToString())}");
        return output.ToString();
    }

    private static void AppendPartyState(
        StringBuilder output,
        string label,
        string owner,
        MobileParty party)
    {
        output.AppendLine(
            $"{label}={owner}|party={party.StringId}|active={party.IsActive}|" +
            $"position={party.Position.X:R},{party.Position.Y:R},{party.Position.IsOnLand}|" +
            $"members={party.MemberRoster.TotalManCount}|prisoners={party.PrisonRoster.TotalManCount}|" +
            $"moveMode={party.PartyMoveMode}|mapEvent={(party.MapEvent?.StringId ?? "none")}");
    }

    private sealed class PostBattleFreezeFixture
    {
        public string FirstControllerId { get; }
        public string SecondControllerId { get; }
        public Settlement Settlement { get; }
        public PartySnapshot FirstPlayer { get; }
        public PartySnapshot SecondPlayer { get; }
        public PartySnapshot AiLord { get; }
        public Hero AiLeader { get; }
        public Hero[] BattleHeroes { get; }
        public HeroSnapshot[] Heroes { get; }
        public ClanSnapshot[] Clans { get; }
        public TimeControlEnum OriginalTimeControl { get; }
        public MobileParty AiParty { get; set; }
        public MapEvent MapEvent { get; set; }
        public bool EncountersOpened { get; set; }
        public CampaignTime ProbeStartedAt { get; set; }

        public PostBattleFreezeFixture(
            string firstControllerId,
            string secondControllerId,
            Settlement settlement,
            PartySnapshot firstPlayer,
            PartySnapshot secondPlayer,
            PartySnapshot aiLord,
            Hero aiLeader,
            Hero[] battleHeroes,
            HeroSnapshot[] heroes,
            ClanSnapshot[] clans,
            TimeControlEnum originalTimeControl)
        {
            FirstControllerId = firstControllerId;
            SecondControllerId = secondControllerId;
            Settlement = settlement;
            FirstPlayer = firstPlayer;
            SecondPlayer = secondPlayer;
            AiLord = aiLord;
            AiLeader = aiLeader;
            BattleHeroes = battleHeroes;
            Heroes = heroes;
            Clans = clans;
            OriginalTimeControl = originalTimeControl;
        }
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party { get; }
        public TroopRosterElement[] MemberRoster { get; }
        public TroopRosterElement[] PrisonRoster { get; }
        public ItemRosterElement[] Items { get; }
        public Hero LeaderHero { get; }
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

[HarmonyPatch(typeof(Hero), nameof(Hero.CanDie))]
internal static class PostBattleFreezeFixtureHeroDeathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Hero __instance,
        KillCharacterAction.KillCharacterActionDetail causeOfDeath,
        ref bool __result)
    {
        if (causeOfDeath != KillCharacterAction.KillCharacterActionDetail.DiedInBattle ||
            !PostBattleFreezeFixtureCommands.IsFixtureHero(__instance))
            return true;

        __result = false;
        return false;
    }
}
#endif
