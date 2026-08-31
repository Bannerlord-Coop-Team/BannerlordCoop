using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Patches;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using SandBox.GauntletUI;
using Serilog;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Party.Commands;

internal class PartyCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyCommands>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// All alive heroes whose full name matches and that have a party (the console splits arguments on spaces,
    /// so the caller joins them back). The bulk cheats act on every match, not just the first, so a co-op test
    /// save with several identically-named heroes - e.g. multiple "RandomPlayer" parties - is set up on all of
    /// them at once. Heroes with no party (a prisoner, notable, or wanderer) are skipped so a cheat never
    /// dereferences a null PartyBelongedTo.
    /// </summary>
    private static List<Hero> FindHeroesWithParty(string name)
        => Hero.AllAliveHeroes.Where(h => h != null && h.Name?.ToString() == name && h.PartyBelongedTo != null).ToList();

    /// <summary>
    /// Finds a single alive hero with a party, for the cheats that target one party (the companion-preserve
    /// pair - putting one hero into several prisons at once would be invalid state). Accepts a hero StringId
    /// (unique, printed by `whoami`) so you can target one specific party when several heroes share a name
    /// (multiple "RandomPlayer" parties), or falls back to a full-name match. Reports cleanly on a miss.
    /// </summary>
    private static bool TryGetHeroWithParty(string nameOrId, out Hero hero, out string error)
    {
        hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == nameOrId)
            ?? Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString() == nameOrId && h.PartyBelongedTo != null);
        if (hero == null) { error = "No hero \"" + nameOrId + "\" (by id or name) with a party found."; return false; }
        if (hero.PartyBelongedTo == null) { error = hero.Name + " (" + hero.StringId + ") has no party (a prisoner, notable, or wanderer?)."; return false; }
        error = null;
        return true;
    }

    /// <summary>
    /// Prints this instance's controlled hero and its ids. Run on a CLIENT to learn your own hero's StringId,
    /// then pass that to imprison_companion / snapshot_prison to target your exact party when several share the
    /// "RandomPlayer" name.
    /// </summary>
    public static string WhoAmICommand(List<string> strings)
    {
        if(!ModInformation.IsClient) return "Command can only be run on a client.";
        
        var me = Hero.MainHero;
        if (me == null) return "No main hero on this instance";

        return me.PartyBelongedTo != null
            ? "You are " + me.Name + " | hero id: " + me.StringId + " | party id: " + me.PartyBelongedTo.StringId
            : "You are " + me.Name + " | hero id: " + me.StringId + " | NO PARTY";
    }

    /// <summary>
    /// Reports an exact party position and movement mode for cross-client live-test comparisons.
    /// </summary>
    public static string PositionCommand(List<string> strings)
    {
        if (strings.Count != 1)
            return "Usage: coop.debug.mobile_party.position <partyId>";

        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out MobileParty party))
            return $"Party with id {strings[0]} not found";

        CampaignVec2 position = party.Position;
        var partyId = objectManager.TryGetId(party, out string resolvedPartyId)
            ? resolvedPartyId
            : strings[0];
        return
            $"id={partyId}|party={party.StringId}|" +
            $"x={position.X.ToString("R", CultureInfo.InvariantCulture)}|" +
            $"y={position.Y.ToString("R", CultureInfo.InvariantCulture)}|" +
            $"isOnLand={position.IsOnLand}|" +
            $"settlement={party.CurrentSettlement?.StringId ?? "none"}|" +
            $"moveMode={party.PartyMoveMode}";
    }

    /// <summary>
    /// Issues a local player-party point movement order so automated live tests can verify that a restored
    /// party accepts client control and sends the normal behavior update to the server.
    /// </summary>
    public static string MoveOffsetCommand(List<string> strings)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (strings.Count != 2 ||
            !float.TryParse(strings[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) ||
            !float.TryParse(strings[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
            return "Usage: coop.debug.mobile_party.move_offset <offsetX> <offsetY>";

        var party = Hero.MainHero?.PartyBelongedTo;
        if (party == null) return "The local player hero has no party.";

        var current = party.Position;
        var target = new CampaignVec2(
            new TaleWorlds.Library.Vec2(current.X + offsetX, current.Y + offsetY),
            current.IsOnLand);
        party.SetNavigationModePoint(target);
        MessageBroker.Instance.Publish(typeof(PartyCommands), new PartyBehaviorChangeAttempted(party));

        return
            $"Movement order submitted for {party.StringId}.\n" +
            $"From: {current.X:R},{current.Y:R}\n" +
            $"Target: {target.X:R},{target.Y:R}";
    }

    /// <summary>
    /// Restores a party to an exact campaign-map position and hold state after an automated live test.
    /// </summary>
    public static string RestorePositionCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (strings.Count != 4 ||
            !float.TryParse(strings[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var positionX) ||
            !float.TryParse(strings[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var positionY) ||
            !bool.TryParse(strings[3], out var isOnLand))
            return "Usage: coop.debug.mobile_party.restore_position <partyId> <x> <y> <isOnLand>";

        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out MobileParty party))
            return $"Party with id {strings[0]} not found";

        party.Position = new CampaignVec2(new TaleWorlds.Library.Vec2(positionX, positionY), isOnLand);
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(PartyCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea,
                resetMovementToHold: true));

        return $"Restored {party.StringId} to {party.Position.X:R},{party.Position.Y:R} in Hold mode.";
    }

    public static string MoveToSettlementCommand(List<string> strings)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";

        if (strings.Count != 2)
            return "Usage: coop.debug.mobile_party.move_to_settlement <partyId> <settlementId>";

        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager.";

        if (!objectManager.TryGetObject(strings[0], out MobileParty party))
            return $"Party with id {strings[0]} not found";

        if (!party.IsPlayerParty())
            return $"Party {party.StringId} is not a player party.";

        var settlement = Settlement.Find(strings[1]);
        if (settlement == null)
            return $"Settlement with id {strings[1]} not found";

        if (!party.IsActive)
            return $"Party {party.StringId} is not active.";

        if (party.CurrentSettlement != null)
            return $"Party {party.StringId} is already in {party.CurrentSettlement.StringId}.";

        var navigationType = party.IsCurrentlyAtSea
            ? MobileParty.NavigationType.Naval
            : MobileParty.NavigationType.Default;
        party.SetMoveGoToSettlement(settlement, navigationType, isTargetingThePort: false);

        return $"Ordered {party.StringId} to {settlement.StringId}.";
    }

    /// <summary>
    /// View character ids in a hero's party by full name or hero id.
    /// </summary>
    public static string ViewItemIdsCommand(List<string> strings)
    {
        if (strings.Count == 0) return "Hero name argument required.";

        var nameOrId = string.Join(" ", strings);
        var mainHero = Hero.MainHero;
        var heroById = mainHero != null &&
                       mainHero.StringId == nameOrId &&
                       mainHero.PartyBelongedTo != null
            ? mainHero
            : Hero.AllAliveHeroes.FirstOrDefault(
                hero => hero != null && hero.StringId == nameOrId && hero.PartyBelongedTo != null);
        var heroes = heroById == null
            ? FindHeroesWithParty(nameOrId)
            : new List<Hero> { heroById };
        if (heroes.Count == 0)
            return "No hero named or identified by \"" + nameOrId + "\" with a party found.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var party = hero.PartyBelongedTo;
            if (party == null) continue;

            stringBuilder.AppendLine("##" + (hero.Name?.ToString() ?? "<unnamed>") + "  (hero id: " + hero.StringId + ")");
            stringBuilder.AppendLine("Member roster:");
            AppendRoster(stringBuilder, party.MemberRoster);

            stringBuilder.AppendLine("Prisoner roster:");
            AppendRoster(stringBuilder, party.PrisonRoster);
        }

        return stringBuilder.ToString();
    }

    private static void AppendRoster(StringBuilder stringBuilder, TroopRoster roster)
    {
        if (roster == null) return;

        for (int index = 0; index < roster.Count; index++)
        {
            var rosterElement = roster.GetElementCopyAtIndex(index);
            stringBuilder.AppendLine(
                rosterElement.Character?.StringId +
                ": number=" + rosterElement.Number +
                " wounded=" + rosterElement.WoundedNumber +
                " xp=" + rosterElement.Xp +
                " hero=" + (rosterElement.Character?.IsHero == true));
        }
    }

    /// <summary>
    /// Sets one member-roster troop's wounded count for live synchronization tests.
    /// </summary>
    public static string SetTroopWoundedCommand(List<string> strings)
    {
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (strings.Count != 3)
            return "Usage: coop.debug.mobile_party.set_troop_wounded <party id> <character id> <wounded count>";

        if (!int.TryParse(strings[2], out var woundedCount))
            return "Please enter an integer for wounded count.";

        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out MobileParty party))
            return $"Party with id {strings[0]} not found.";
        if (!objectManager.TryGetObject(strings[1], out CharacterObject character))
            return $"Character with id {strings[1]} not found.";

        var roster = party.MemberRoster;
        var index = roster.FindIndexOfTroop(character);
        if (index < 0)
            return $"{strings[1]} is not in {party.Name}'s member roster.";

        var element = roster.GetElementCopyAtIndex(index);
        if (woundedCount < 0 || woundedCount > element.Number)
            return $"Wounded count must be between 0 and {element.Number}.";

        roster.SetElementWoundedNumber(index, woundedCount);
        return $"Set {strings[1]} oldWounded={element.WoundedNumber} newWounded={woundedCount}.";
    }

    /// <summary>
    /// Sets one member-roster troop's exact state and reports the state it replaced.
    /// </summary>
    public static string SetTroopStateCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        string validationError = ValidateTroopStateArgs(
            strings,
            out var shouldExist,
            out var number,
            out var woundedCount,
            out var xp);
        if (validationError != null) return validationError;

        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out MobileParty party))
            return $"Party with id {strings[0]} not found.";
        if (!objectManager.TryGetObject(strings[1], out CharacterObject character))
            return $"Character with id {strings[1]} not found.";
        if (character.IsHero) return "Hero roster elements are not supported by this command.";

        var roster = party.MemberRoster;
        int index = roster.FindIndexOfTroop(character);
        bool oldExists = index >= 0;
        var oldState = oldExists ? roster.GetElementCopyAtIndex(index) : default;
        int oldNumber = oldExists ? oldState.Number : 0;
        int oldWounded = oldExists ? oldState.WoundedNumber : 0;
        int oldXp = oldExists ? oldState.Xp : 0;

        if (index < 0 && shouldExist)
        {
            roster.AddToCounts(character, System.Math.Max(number, 1), removeDepleted: false);
            index = roster.FindIndexOfTroop(character);
        }

        if (index >= 0)
        {
            int currentWounded = roster.GetElementWoundedNumber(index);
            if (currentWounded > number) roster.SetElementWoundedNumber(index, number);
            roster.SetElementNumber(index, number);
            roster.SetElementWoundedNumber(index, woundedCount);
            roster.SetElementXp(index, xp);
            if (!shouldExist) roster.RemoveZeroCounts();
            roster.InitializeCachedData();
        }

        return $"TROOP_STATE_SET party={strings[0]} character={strings[1]} " +
               $"oldExists={oldExists} oldNumber={oldNumber} oldWounded={oldWounded} oldXp={oldXp} " +
               $"newExists={shouldExist} newNumber={number} newWounded={woundedCount} newXp={xp}";
    }

    private static string ValidateTroopStateArgs(
        List<string> strings,
        out bool shouldExist,
        out int number,
        out int woundedCount,
        out int xp)
    {
        shouldExist = false;
        number = 0;
        woundedCount = 0;
        xp = 0;
        if (strings.Count != 6)
            return "Usage: coop.debug.mobile_party.set_troop_state <party id> <character id> <exists> <number> <wounded count> <xp>";

        if (!bool.TryParse(strings[2], out shouldExist) ||
            !int.TryParse(strings[3], out number) ||
            !int.TryParse(strings[4], out woundedCount) ||
            !int.TryParse(strings[5], out xp))
            return "Exists must be true or false, and number, wounded count, and xp must be integers.";
        if (number < 0 || woundedCount < 0 || woundedCount > number || xp < 0 || (number == 0 && xp != 0) ||
            (!shouldExist && (number != 0 || woundedCount != 0 || xp != 0)))
            return "State requires number >= 0, wounded between 0 and number, xp >= 0, zero xp when number is zero, and all zero values when exists is false.";

        return null;
    }

    /// <summary>
    /// Selects a real right-member row so live tests can observe its inline upgrade choices.
    /// </summary>
    public static string SelectPartyScreenTroopCommand(List<string> strings)
    {
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (strings.Count != 1)
            return "Usage: coop.debug.mobile_party.select_party_screen_troop <character id>";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out CharacterObject character))
            return $"Character with id {strings[0]} not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState))
            return "No active party screen.";

        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        if (partyVm == null) return "No active Party screen view model.";

        var row = partyVm.MainPartyTroops.FirstOrDefault(vm => vm.Character == character);
        if (row == null) return $"{strings[0]} is not in the right member roster.";

        if (!row.IsSelected) partyVm.ExecuteSelectCharacterTuple(row);
        return $"PARTY_SCREEN_TROOP_SELECTED character={strings[0]} selected={row.IsSelected} " +
               $"upgradeTargets={row.Upgrades.Count} ready={row.NumOfReadyToUpgradeTroops} " +
               $"upgradeable={row.NumOfUpgradeableTroops}";
    }

    /// <summary>
    /// Applies the same upgrade command created by the Party-screen row.
    /// </summary>
    public static string UpgradePartyScreenTroopCommand(List<string> strings)
    {
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (strings.Count < 1 || strings.Count > 2)
            return "Usage: coop.debug.mobile_party.upgrade_party_screen_troop <character id> [upgrade target index]";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out CharacterObject character))
            return $"Character with id {strings[0]} not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState))
            return "No active party screen.";

        int upgradeTarget = 0;
        if (strings.Count == 2 && (!int.TryParse(strings[1], out upgradeTarget) || upgradeTarget < 0))
            return "Upgrade target index must be a non-negative integer.";
        if (upgradeTarget >= character.UpgradeTargets.Length)
            return $"{strings[0]} has {character.UpgradeTargets.Length} upgrade targets.";

        var logic = partyState.PartyScreenLogic;
        int rosterIndex = logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right]
            .FindIndexOfTroop(character);
        if (rosterIndex < 0) return $"{strings[0]} is not in the right member roster.";

        var troop = logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right]
            .GetElementCopyAtIndex(rosterIndex);
        int insertionIndex = logic.GetIndexToInsertTroop(
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member,
            troop);
        var command = new PartyScreenLogic.PartyCommand();
        command.FillForUpgradeTroop(
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member,
            character,
            1,
            upgradeTarget,
            insertionIndex);
        if (!logic.ValidateCommand(command))
            return $"PARTY_SCREEN_UPGRADE_REJECTED character={strings[0]} target={upgradeTarget}";

        logic.AddCommand(command);
        return $"PARTY_SCREEN_UPGRADE_STAGED character={strings[0]} " +
            $"target={character.UpgradeTargets[upgradeTarget].StringId} pending={logic.IsThereAnyChanges()}";
    }

    /// <summary>
    /// Creates a real pending Party-screen transfer for live synchronization tests.
    /// </summary>
    public static string StagePartyScreenTransferCommand(List<string> strings)
    {
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (strings.Count != 1)
            return "Usage: coop.debug.mobile_party.stage_party_screen_transfer <character id>";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out CharacterObject character))
            return $"Character with id {strings[0]} not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState))
            return "No active party screen.";

        var logic = partyState.PartyScreenLogic;
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        if (partyVm == null) return "No active Party screen view model.";

        var row = partyVm.MainPartyTroops.FirstOrDefault(vm => vm.Character == character);
        if (row == null) return $"{strings[0]} is not in the right member roster.";

        partyVm.OnTransferTroop(row, -1, 1, row.Side);
        partyVm.ExecuteRemoveZeroCounts();
        return $"PARTY_SCREEN_EDIT_STAGED pending={logic.IsThereAnyChanges()}";
    }

    /// <summary>
    /// Reports the visible roster, Done baseline, and rendered VM state for one open Party-screen row.
    /// </summary>
    public static string PartyScreenTroopStateCommand(List<string> strings)
    {
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (strings.Count != 1)
            return "Usage: coop.debug.mobile_party.party_screen_troop_state <character id>";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out CharacterObject character))
            return $"Character with id {strings[0]} not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState))
            return "No active party screen.";

        var logic = partyState.PartyScreenLogic;
        var visible = GetRosterElement(logic.MemberRosters[1], character);
        var baseline = GetRosterElement(logic._initialData.RightMemberRoster, character);
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        var row = partyVm?.MainPartyTroops.FirstOrDefault(vm => vm.Character == character);
        var rendered = row == null
            ? (number: -1, wounded: -1, xp: -1)
            : (number: row.Troop.Number, wounded: row.Troop.WoundedNumber, xp: row.Troop.Xp);

        return $"PARTY_SCREEN_TROOP_STATE character={strings[0]} " +
               $"visibleNumber={visible.number} visibleWounded={visible.wounded} visibleXp={visible.xp} " +
               $"baselineNumber={baseline.number} baselineWounded={baseline.wounded} baselineXp={baseline.xp} " +
               $"vmNumber={rendered.number} vmWounded={rendered.wounded} vmXp={rendered.xp} " +
               $"selected={row?.IsSelected == true} upgradeTargets={row?.Upgrades.Count ?? 0} " +
               $"ready={row?.NumOfReadyToUpgradeTroops ?? 0} upgradeable={row?.NumOfUpgradeableTroops ?? 0} " +
               $"pending={logic.IsThereAnyChanges()}";
    }

    private static (int number, int wounded, int xp) GetRosterElement(
        TroopRoster roster,
        CharacterObject character)
    {
        var index = roster.FindIndexOfTroop(character);
        if (index < 0) return (-1, -1, -1);

        var element = roster.GetElementCopyAtIndex(index);
        return (element.Number, element.WoundedNumber, element.Xp);
    }

    /// <summary>
    /// Add xp to all troops in a hero's party
    /// </summary>
    public static string AddTroopXpCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count < 2) return "Hero name and xp amount required.";

        // The xp amount is the last token; the rest is the (possibly multi-word) hero name.
        if (!int.TryParse(strings[strings.Count - 1], out int xpGain)) return "Please enter an integer for xp amount";

        var name = string.Join(" ", strings.Take(strings.Count - 1));
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        foreach (var hero in heroes)
        {
            var memberRoster = hero.PartyBelongedTo.MemberRoster;
            foreach (var troop in memberRoster.data)
            {
                memberRoster.AddXpToTroop(troop.Character, xpGain);
            }
        }

        return "Gave xp to the troops of " + heroes.Count + " party/parties named \"" + name + "\".";
    }

    /// <summary>
    /// Add troops to a hero's party
    /// </summary>
    public static string AddRecruitsCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        var troopsToAdd = new Dictionary<string, int>()
        {
            { "imperial_vigla_recruit", 5 },
            { "imperial_recruit", 2 },
            { "imperial_equite", 2 },
            { "imperial_heavy_horseman", 2 }
        };

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var memberRoster = hero.PartyBelongedTo.MemberRoster;
            foreach (var troopId in troopsToAdd.Keys)
            {
                if (!objectManager.TryGetObject(troopId, out CharacterObject characterObject))
                {
                    stringBuilder.AppendLine("Failed to retrieve object for CharacterObject id: " + troopId);
                }
                else
                {
                    memberRoster.AddToCounts(characterObject, troopsToAdd[troopId]);
                }
            }

            stringBuilder.AppendLine(hero.Name.ToString() + " was given troops.");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.mobile_party.siege_buff
    /// <summary>
    /// Fills a party to 2000 troops, maxes its morale, forces a high map speed, and stocks it with food so it
    /// can march to and win a siege for testing without starving. Server only; the troop and item adds replicate
    /// via the roster sync. Get the party id from coop.debug.mobile_party.who_am_i on the client that owns the party.
    /// </summary>
    public static string SiegeBuffCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (strings.Count != 1) return "Usage: coop.debug.mobile_party.siege_buff <partyId>";
        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out MobileParty party)) return $"Party with id {strings[0]} not found";

        var troop = party.MapFaction?.Culture?.EliteBasicTroop ?? party.MapFaction?.Culture?.BasicTroop;
        if (troop == null) return $"Could not resolve a troop for {party.Name}'s culture";

        int toAdd = 2000 - party.MemberRoster.TotalManCount;
        if (toAdd > 0) party.MemberRoster.AddToCounts(troop, toAdd);

        party.RecentEventsMorale = 100f;
        PartyDebugBuffPatches.Boost(party);

        // Stock every food type so a 2000-troop army doesn't starve on the march to the siege. AddToCounts routes
        // through the synced EquipmentElement overload, so the food replicates to the owning client.
        int foodTypes = 0;
        foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
        {
            if (item?.IsFood != true) continue;
            party.ItemRoster.AddToCounts(item, 500);
            foodTypes++;
        }

        return $"Buffed {party.Name} ({party.StringId}): {party.MemberRoster.TotalManCount} troops, max morale, boosted speed and party-size limit, {foodTypes} food type(s) x500";
    }

    // coop.debug.mobile_party.declare_war
    /// <summary>
    /// Declares war between a party's faction and a settlement's faction, so the party can besiege that
    /// settlement. Works for an independent clan (no kingdom needed). Server only; the war replicates.
    /// </summary>
    public static string DeclareWarCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (strings.Count != 2) return "Usage: coop.debug.mobile_party.declare_war <partyId> <settlementId>";
        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(strings[0], out MobileParty party)) return $"Party with id {strings[0]} not found";
        if (!objectManager.TryGetObject(strings[1], out Settlement settlement)) return $"Settlement with id {strings[1]} not found";

        var attacker = party.MapFaction;
        var defender = settlement.MapFaction;
        if (attacker == null || defender == null) return "Could not resolve both factions";
        if (attacker == defender) return "The party and the settlement share a faction";
        if (attacker.IsAtWarWith(defender)) return $"{attacker.Name} is already at war with {defender.Name}";

        DeclareWarAction.ApplyByDefault(attacker, defender);
        return $"{attacker.Name} is now at war with {defender.Name}";
    }

    /// <summary>
    /// Add prisoners to a hero's party
    /// </summary>
    public static string AddPrisonersCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        var troopsToAdd = new Dictionary<string, int>()
        {
            { "imperial_vigla_recruit", 5 },
            { "imperial_recruit", 2 },
            { "imperial_equite", 2 },
            { "imperial_heavy_horseman", 2 }
        };

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var prisonerRoster = hero.PartyBelongedTo.PrisonRoster;
            foreach (var troopId in troopsToAdd.Keys)
            {
                if (!objectManager.TryGetObject(troopId, out CharacterObject characterObject))
                {
                    stringBuilder.AppendLine("Failed to retrieve object for CharacterObject id: " + troopId);
                }
                else
                {
                    prisonerRoster.AddToCounts(characterObject, troopsToAdd[troopId]);
                }
            }

            stringBuilder.AppendLine(hero.Name.ToString() + " was given prisoners.");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Remove all prisoners from a hero's party
    /// </summary>
    public static string RemovePrisonersCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        var name = string.Join(" ", strings);
        var heroes = FindHeroesWithParty(name);
        if (heroes.Count == 0) return "No hero named \"" + name + "\" with a party found.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in heroes)
        {
            var prisonerRoster = hero.PartyBelongedTo.PrisonRoster;

            // Walk from the end so removing the current element leaves the lower indices valid. Each
            // subtract-to-zero with removeDepleted runs with patches live, so it replicates to clients.
            for (int i = prisonerRoster.Count - 1; i >= 0; i--)
            {
                var element = prisonerRoster.GetElementCopyAtIndex(i);
                prisonerRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber, 0, true);
            }

            stringBuilder.AppendLine(hero.Name.ToString() + " had their prisoners removed.");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Put a hero (e.g. a player companion) into a hero's party prison roster, to set up the
    /// companion-preserve test. Args: captor hero name, prisoner hero name.
    /// </summary>
    public static string ImprisonCompanionCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count < 2) return "Captor hero name and prisoner hero name required.";

        // The console splits arguments on spaces, so the captor is the first token and the prisoner name is
        // the rest joined back together. Companions always have a multi-word name (e.g. "Chandion the Bull"),
        // which would otherwise arrive as several tokens and never match. (The captor must be a single-token
        // name for this split to work, which the player's own hero typically is.) One captor only: a hero can
        // only be a prisoner in one place, so imprisoning the companion in several prisons would be invalid.
        if (!TryGetHeroWithParty(strings[0], out var captor, out var error)) return error;

        var prisonerName = string.Join(" ", strings.Skip(1));
        var prisoner = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString() == prisonerName);
        if (prisoner == null) return "Prisoner hero \"" + prisonerName + "\" not found.";

        // The preserve only fires for a player companion, so a non-companion would be removed by both the
        // fixed and the old code and prove nothing. Require a companion so the test actually exercises it.
        if (!prisoner.IsPlayerCompanion) return prisoner.Name + " is not a player companion; this test needs one.";

        // Place a copy of the companion in the prison roster as a test fixture for the snapshot path.
        // Deliberately a raw AddToCounts, NOT a TakePrisonerAction: the full imprisonment doesn't replicate
        // cleanly in co-op (it zeroes the prisoner's home party on its owning client - a separate captivity
        // sync bug). The companion stays in its own party, so the snapshot test only ever clears the prison
        // copy and there is nothing to restore afterwards.
        captor.PartyBelongedTo.PrisonRoster.AddToCounts(prisoner.CharacterObject, 1);
        return prisoner.Name + " (a player companion) placed in " + captor.Name + "'s prison roster (a test copy; it stays in its own party).";
    }

    /// <summary>
    /// Apply a whole-roster snapshot to a hero's party prison roster with the hero prisoners stripped out, as
    /// if the server freed them. Drives TroopRosterInterface.UpdateWithData on a prison roster: hero prisoners
    /// (player companions included) must be removed, not preserved, and the removal replicates to clients.
    /// </summary>
    public static string SnapshotPrisonCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        // One party only, to pair with imprison_companion (which targets one prison).
        if (!TryGetHeroWithParty(string.Join(" ", strings), out var hero, out var error)) return error;

        var prisonRoster = hero.PartyBelongedTo.PrisonRoster;
        if (ContainerProvider.TryGetContainer(out var container) == false ||
            container.TryResolve(out ITroopRosterInterface troopRosterInterface) == false)
            return "Unable to resolve TroopRosterInterface.";

        // Pack the prison roster, then drop the hero elements so the snapshot no longer carries them, as if
        // the server had freed them. Resolve each element's CharacterObject to tell heroes from basic troops.
        var packed = troopRosterInterface.PackTroopRosterData(prisonRoster);
        var nonHeroElements = new List<TroopRosterElementData>();
        foreach (var element in packed.Data)
        {
            if (objectManager.TryGetObject(element.CharacterId, out CharacterObject troop) && troop.IsHero) continue;
            nonHeroElements.Add(element);
        }
        var snapshot = new TroopRosterData(nonHeroElements);

        // Pass a non-null mainHero so the preserve decision turns on the prison-vs-member roster check (the
        // thing under test), not on a null mainHero short-circuit.
        troopRosterInterface.UpdateWithData(prisonRoster, snapshot, hero);

        int heroesLeft = 0;
        for (int i = 0; i < prisonRoster.Count; i++)
        {
            if (prisonRoster.GetElementCopyAtIndex(i).Character?.IsHero == true) heroesLeft++;
        }

        return heroesLeft == 0
            ? "Applied prison snapshot to " + hero.Name + "; all hero prisoners removed (companion-preserve correctly off for prison rosters)."
            : "Applied prison snapshot to " + hero.Name + "; " + heroesLeft + " hero prisoner(s) still present (companion-preserve wrongly kept them).";
    }
}
