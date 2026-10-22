#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using Helpers;
using NavalDLC.GauntletUI.Screens;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Coop.Naval.PortScreen.Commands;

// coop.debug.naval.finish_loot
// coop.debug.naval.finish_loot take=all
// coop.debug.naval.finish_loot take=Ship_MobileParty_southern_pirates_371_1
/// <summary>
/// DEBUG: finishes the client's open post-battle loot screens through their own buttons, as the player would: takes every troop,
/// prisoner and item, then takes the chosen loot ships, leaves the rest and confirms.
/// </summary>
public sealed class NavalFinishLootCoopCommand : ICoopCommand
{
    internal const string Label = "NAVAL_FINISH_LOOT";

    // The party, inventory and ship screens, so a screen that never closes can't loop.
    private const int MaxScreens = 3;

    // A button raises at most a couple of confirmations (leaving loot behind, over limit).
    private const int MaxInquiries = 4;

    private readonly IObjectManager objectManager;

    // Takes the object manager that names the offered ships by their registry ids.
    public NavalFinishLootCoopCommand(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "finish_loot";

    public string Description => "Finishes the open post-battle loot screens with their own buttons: takes all troops, prisoners and " +
        "items, then the chosen loot ships, and leaves the rest.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("take", "take=<shipIdPrefix[,shipIdPrefix...]|none|all>, the loot ships to take; none when omitted.", false),
    };

    // Finishes each loot screen on top in turn until none is left or the next opens on a later frame.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsClient) return Failed("Run this command on a client.");
        if (!TryParseTake(args, out var take, out var error)) return Failed(error);

        var steps = new List<string>();
        for (int screen = 0; screen < MaxScreens; screen++)
        {
            var state = Game.Current?.GameStateManager?.ActiveState;
            error = FinishScreen(state, take, out var step);
            if (error != null) return Failed(string.Join("; ", steps.Concat(new[] { error })));
            if (step == null) break;

            steps.Add(step);
            if (Game.Current?.GameStateManager?.ActiveState == state) break;
        }

        var active = Game.Current?.GameStateManager?.ActiveState;
        if (steps.Count == 0) return Failed($"No post-battle loot screen is open (active state {active?.GetType().Name ?? "none"}).");

        return Succeeded($"{Label} {string.Join("; ", steps)}; now {active?.GetType().Name ?? "none"} " +
            $"(run again if the next loot screen opens on a later frame)");
    }

    /// <summary>The loot ships to take: every one, none, or the ones the listed id prefixes pick.</summary>
    internal sealed class LootTake
    {
        public static readonly LootTake None = new LootTake(false, Array.Empty<string>());

        // Assigns the selection.
        public LootTake(bool all, IReadOnlyList<string> prefixes)
        {
            All = all;
            Prefixes = prefixes;
        }

        public bool All { get; }
        public IReadOnlyList<string> Prefixes { get; }
    }

    /// <summary>Parses the optional <c>take=</c> argument; false with the usage when it is malformed.</summary>
    internal static bool TryParseTake(IReadOnlyList<string> args, out LootTake take, out string error)
    {
        take = LootTake.None;
        error = null;
        if (args == null || args.Count == 0) return true;

        const string key = "take=";
        string arg = args[0]?.Trim() ?? "";
        if (args.Count > 1 || !arg.StartsWith(key, StringComparison.OrdinalIgnoreCase) || arg.Length == key.Length)
        {
            error = "Usage: coop.debug.naval.finish_loot [take=<shipIdPrefix[,shipIdPrefix...]|none|all>]";
            return false;
        }

        string value = arg.Substring(key.Length);
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            take = new LootTake(true, Array.Empty<string>());
            return true;
        }

        var prefixes = value.Split(',').Select(prefix => prefix.Trim()).ToArray();
        if (prefixes.Any(string.IsNullOrEmpty))
        {
            error = "take lists an empty ship id prefix.";
            return false;
        }

        take = new LootTake(false, prefixes);
        return true;
    }

    /// <summary>
    /// The offered ship ids to take: each prefix picks the id it equals, else the one id that starts with it (either with or
    /// without the "Ship_" prefix); null, or why a prefix picks none or several.
    /// </summary>
    internal static string TryResolveTake(LootTake take, IReadOnlyList<string> offeredIds, out List<string> taken)
    {
        taken = new List<string>();
        if (take.All)
        {
            taken.AddRange(offeredIds);
            return null;
        }

        foreach (var prefix in take.Prefixes)
        {
            var exact = offeredIds.Where(id => IsId(id, prefix) || IsId(StripShip(id), prefix)).ToArray();
            var matches = exact.Length > 0
                ? exact
                : offeredIds.Where(id => StartsWith(id, prefix) || StartsWith(StripShip(id), prefix)).ToArray();
            if (matches.Length != 1)
                return $"take {prefix} matches {matches.Length} offered ships ({string.Join(", ", offeredIds)}).";
            if (!taken.Contains(matches[0])) taken.Add(matches[0]);
        }

        return null;
    }

    // Case-insensitive id equality.
    private static bool IsId(string id, string other) => string.Equals(id, other, StringComparison.OrdinalIgnoreCase);

    // Case-insensitive prefix test.
    private static bool StartsWith(string id, string prefix) => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    // The ship id without its registry type prefix.
    private static string StripShip(string id) => id.StartsWith("Ship_", StringComparison.Ordinal) ? id.Substring("Ship_".Length) : id;

    // Finishes the loot screen <paramref name="state"/> shows; step is null when it shows none, the error is null on success.
    private string FinishScreen(TaleWorlds.Core.GameState state, LootTake take, out string step)
    {
        step = null;
        if (state is PartyState party && party.PartyScreenMode == PartyScreenHelper.PartyScreenMode.Loot) return FinishParty(party, out step);
        if (state is InventoryState inventory && inventory.InventoryMode == InventoryScreenHelper.InventoryMode.Loot) return FinishInventory(out step);
        if (state is PortState port && port.PortScreenMode == PortScreenModes.LootMode) return FinishShips(port, take, out step);

        return null;
    }

    // Takes every troop and prisoner on the loot side (the screen's take-all buttons), then presses Done.
    private static string FinishParty(PartyState state, out string step)
    {
        step = null;
        if (!((ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource is { } partyVm)) return "The loot party view model is unavailable.";

        var logic = state.PartyScreenLogic;
        int troops = logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left].TotalManCount;
        int prisoners = logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left].TotalManCount;
        partyVm.ExecuteTransferAllOtherTroops();
        partyVm.ExecuteTransferAllOtherPrisoners();
        int answered = RunAcceptingInquiries(partyVm.ExecuteDone);
        step = $"party: took {troops} troops and {prisoners} prisoners offered, Done (yes to {answered} prompts)";
        return null;
    }

    // Takes every item on the loot side (the screen's take-all button), then presses Done.
    private static string FinishInventory(out string step)
    {
        step = null;
        if (!((ScreenManager.TopScreen as GauntletInventoryScreen)?._dataSource is { } inventoryVm)) return "The loot inventory view model is unavailable.";

        inventoryVm.ExecuteBuyAllItems();
        int answered = RunAcceptingInquiries(inventoryVm.ExecuteCompleteTranstactions);
        step = $"inventory: took all items, Done (yes to {answered} prompts)";
        return null;
    }

    // Selects and takes each chosen loot ship (its card, then Take), then presses Confirm; the ships left behind are discarded.
    private string FinishShips(PortState state, LootTake take, out string step)
    {
        step = null;
        if (!((ScreenManager.TopScreen as GauntletPortScreen)?._dataSource is { } portVm)) return "The loot port view model is unavailable.";

        var offered = new Dictionary<string, Ship>();
        foreach (var ship in (IEnumerable<Ship>)state.LeftShips ?? Enumerable.Empty<Ship>())
            offered[objectManager.TryGetId(ship, out var shipId) ? shipId : ship.Name.ToString()] = ship;

        var error = TryResolveTake(take, offered.Keys.ToList(), out var taken);
        if (error != null) return error;

        foreach (var shipId in taken)
        {
            var card = portVm.AllShips.FirstOrDefault(item => item.Ship == offered[shipId]);
            if (card == null) return $"The loot screen has no card for {shipId}.";

            card.ExecuteSelect();
            portVm.ExecuteBuy();
        }

        int answered = RunAcceptingInquiries(portVm.ExecuteConfirm);
        var left = offered.Keys.Except(taken).ToArray();
        step = $"ships: took [{string.Join(", ", taken)}], left [{string.Join(", ", left)}], Confirm (yes to {answered} prompts)";
        return null;
    }

    // Presses a button and answers yes to each confirmation it raises (leaving loot behind, over limit), as the player would.
    private static int RunAcceptingInquiries(Action press)
    {
        var raised = new Queue<InquiryData>();
        Action<InquiryData, bool, bool> capture = (inquiry, _, _) => raised.Enqueue(inquiry);
        InformationManager.OnShowInquiry += capture;
        int answered = 0;
        try
        {
            press();
            while (raised.Count > 0 && answered < MaxInquiries)
            {
                var inquiry = raised.Dequeue();
                InformationManager.HideInquiry();
                answered++;
                inquiry.AffirmativeAction?.Invoke();
            }
        }
        finally
        {
            InformationManager.OnShowInquiry -= capture;
        }

        return answered;
    }

    // A successful command result.
    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);

    // A failed command result.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
