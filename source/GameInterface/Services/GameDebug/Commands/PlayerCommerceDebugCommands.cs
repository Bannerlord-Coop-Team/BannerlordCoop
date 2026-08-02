#if DEBUG
using Common;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Tutorial;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

internal class PlayerCommerceDebugCommands
{
    [CommandLineArgumentFunction("state", "coop.debug.playercommerce")]
    public static string State(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.playercommerce.state <partyId>";

        var party = Campaign.Current?.CampaignObjectManager.Find<MobileParty>(args[0]);
        if (party == null)
            return $"Party with id {args[0]} not found.";

        var leader = party.LeaderHero;
        var owner = party.LordPartyComponent?.Owner;
        var grain = Game.Current?.ObjectManager.GetObject<ItemObject>("grain");
        var grainCount = grain == null ? -1 : party.ItemRoster.GetItemNumber(grain);

        return
            $"party={party.StringId}|" +
            $"leader={leader?.StringId ?? "none"}|" +
            $"owner={owner?.StringId ?? "none"}|" +
            $"members={party.MemberRoster.TotalManCount}|" +
            $"grain={grainCount}|" +
            $"gold={leader?.Gold ?? -1}|" +
            $"settlement={party.CurrentSettlement?.StringId ?? "none"}|" +
            $"x={party.Position.X:R}|" +
            $"y={party.Position.Y:R}|" +
            $"isOnLand={party.Position.IsOnLand}|" +
            $"saving={Campaign.Current?.SaveHandler?.IsSaving ?? false}|" +
            $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}|" +
            $"activeState={Game.Current?.GameStateManager.ActiveState?.GetType().Name ?? "none"}";
    }

    [CommandLineArgumentFunction("enter_danustica", "coop.debug.playercommerce")]
    public static string EnterDanustica(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Command can only be run on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.playercommerce.enter_danustica <partyId>";

        var party = Campaign.Current?.CampaignObjectManager.Find<MobileParty>(args[0]);
        if (party == null)
            return $"Party with id {args[0]} not found.";

        var danustica = Settlement.Find("town_ES1");
        if (danustica == null)
            return "Danustica (town_ES1) was not found.";
        if (!TryFinalizeIdleMapEvent(party, out var mapEventError))
            return mapEventError;
        if (party.CurrentSettlement != null && party.CurrentSettlement != danustica)
            LeaveSettlementAction.ApplyForParty(party);
        if (party.CurrentSettlement != danustica)
            EnterSettlementAction.ApplyForParty(party, danustica);
        if (party.CurrentSettlement != danustica)
            return $"Failed to place {party.StringId} in Danustica (town_ES1).";

        return $"Placed {party.StringId} in Danustica (town_ES1).";
    }

    private static bool TryFinalizeIdleMapEvent(MobileParty party, out string error)
    {
        var mapEvent = party.MapEvent;
        if (mapEvent == null)
        {
            error = null;
            return true;
        }

        if (mapEvent.IsFinalized)
        {
            error = $"Party {party.StringId} is still attached to a finalized map event.";
            return false;
        }
        if (mapEvent.BattleState != BattleState.None)
        {
            error = $"Refusing to finalize map event with battle state {mapEvent.BattleState}.";
            return false;
        }
        if (mapEvent.MapEventSettlement != null || mapEvent.BattleObserver != null)
        {
            error = "Refusing to finalize a settlement or active simulation map event.";
            return false;
        }

        mapEvent.FinalizeEvent();
        if (party.MapEvent != null || party.Party?.MapEventSide != null)
        {
            error = $"Party {party.StringId} remained attached to its map event after finalization.";
            return false;
        }

        error = null;
        return true;
    }

    [CommandLineArgumentFunction("open_danustica_town", "coop.debug.playercommerce")]
    public static string OpenDanusticaTown(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Command can only be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.playercommerce.open_danustica_town";

        var party = MobileParty.MainParty;
        var danustica = Settlement.Find("town_ES1");
        if (party?.CurrentSettlement != danustica)
            return "The client party is not in Danustica (town_ES1).";

        if (PlayerEncounter.Current == null)
            EncounterManager.StartSettlementEncounter(party, danustica);
        if (PlayerEncounter.EncounterSettlement != danustica)
            return "A player encounter with another settlement is active.";

        PlayerEncounter.EnterSettlement();
        GameMenu.SwitchToMenu("town");
        return "Opened the Danustica town menu.";
    }

    [CommandLineArgumentFunction("open_town_option", "coop.debug.playercommerce")]
    public static string OpenTownOption(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Command can only be run on a client.";
        if (args.Count != 1 || (args[0] != "trade" && args[0] != "recruit_volunteers"))
            return "Usage: coop.debug.playercommerce.open_town_option <trade|recruit_volunteers>";

        var menuContext = Campaign.Current?.CurrentMenuContext;
        var menu = menuContext?.GameMenu;
        if (menu?.StringId != "town")
            return $"The current menu is '{menu?.StringId ?? "none"}', expected 'town'.";

        var option = menu.MenuOptions.FirstOrDefault(candidate => candidate.IdString == args[0]);
        if (option == null)
            return $"Town option '{args[0]}' was not found.";

        option.RunConsequence(menuContext);
        return $"Opened Danustica town option '{args[0]}'.";
    }

    [CommandLineArgumentFunction("buy_grain", "coop.debug.playercommerce")]
    public static string BuyGrain(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Command can only be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.playercommerce.buy_grain";

        if (Game.Current?.GameStateManager.ActiveState is not InventoryState inventoryState ||
            inventoryState.InventoryLogic == null)
        {
            return "The trade inventory is not open.";
        }

        var inventoryLogic = inventoryState.InventoryLogic;
        var grain = inventoryLogic
            .GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)
            .FirstOrDefault(element => element.EquipmentElement.Item?.StringId == "grain");
        if (grain.EquipmentElement.Item == null || grain.Amount < 1)
            return "Danustica's market has no grain available.";

        var command = TransferCommand.Transfer(
            1,
            InventoryLogic.InventorySide.OtherInventory,
            InventoryLogic.InventorySide.PlayerInventory,
            grain,
            EquipmentIndex.None,
            EquipmentIndex.None,
            null);
        inventoryLogic.AddTransferCommand(command);
        InventoryScreenHelper.CloseScreen(fromCancel: false);

        return "Submitted one grain purchase through the active trade inventory.";
    }

    [CommandLineArgumentFunction("recruit_one", "coop.debug.playercommerce")]
    public static string RecruitOne(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Command can only be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.playercommerce.recruit_one";
        if (Settlement.CurrentSettlement?.StringId != "town_ES1")
            return "The client is not in Danustica (town_ES1).";

        var recruitment = new RecruitmentVM();
        try
        {
            if (recruitment.CurrentPartySize >= recruitment.PartyCapacity)
                return "The player party is already at capacity.";

            var recruit = recruitment.VolunteerList
                .SelectMany(owner => owner.Troops)
                .FirstOrDefault(troop => troop.CanBeRecruited);
            if (recruit == null)
                return "Danustica has no recruitable volunteer.";

            var notableId = recruit.Owner.OwnerHero.StringId;
            var characterId = recruit.Character.StringId;
            var index = recruit.Index;
            recruit.ExecuteRecruit();
            recruitment.ExecuteDone();

            return $"Submitted recruitment notable={notableId}|character={characterId}|index={index}.";
        }
        finally
        {
            FinalizeTemporaryRecruitment(recruitment);
        }
    }

    private static void FinalizeTemporaryRecruitment(RecruitmentVM recruitment)
    {
        // Vanilla OnFinalize assumes the Gauntlet view initialized its input-key VMs.
        RecruitVolunteerTroopVM.OnFocused = (Action<RecruitVolunteerTroopVM>)Delegate.Remove(
            RecruitVolunteerTroopVM.OnFocused,
            new Action<RecruitVolunteerTroopVM>(recruitment.OnVolunteerTroopFocusChanged));
        RecruitVolunteerOwnerVM.OnFocused = (Action<RecruitVolunteerOwnerVM>)Delegate.Remove(
            RecruitVolunteerOwnerVM.OnFocused,
            new Action<RecruitVolunteerOwnerVM>(recruitment.OnVolunteerOwnerFocusChanged));
        Game.Current.EventManager.UnregisterEvent<TutorialNotificationElementChangeEvent>(
            recruitment.OnTutorialNotificationElementIDChange);
    }
}
#endif
