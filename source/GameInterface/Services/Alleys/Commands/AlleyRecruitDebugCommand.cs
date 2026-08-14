using Common;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Messages;
using Helpers;
using SandBox.CampaignBehaviors;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using static GameInterface.Services.ObjectManager.ObjectManager;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Alleys.Commands;

/// <summary>DEBUG commands for the exact alley-recruitment inventory, trade, and loot regression.</summary>
public class AlleyRecruitDebugCommand
{
    private const string AskForVolunteersOptionId = "alley_talk_start_player_owned_alley_manager_answer_2";
    private const string AcceptVolunteersOptionId = "alley_talk_start_player_owned_alley_manager_volunteers_3";

    private static AlleyRecruitFixture fixture;

    [CommandLineArgumentFunction("recruit_fixture_start", "coop.debug.alley")]
    public static string StartFixture(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 3)
            return "Usage: coop.debug.alley.recruit_fixture_start <settlementId> <alleyIndex> <heroRegistryId>";
        if (fixture != null) return "The alley recruit fixture is already active.";

        if (!TryGetAlley(args[0], args[1], out var alley, out var error)) return error;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<ISessionAlleyPlayerDataInterface>(out var sessionInterface) ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve the alley recruit fixture services.";
        if (!objectManager.TryGetObjectWithLogging<Hero>(args[2], out var owner)) return $"Hero '{args[2]}' not found.";
        if (owner.PartyBelongedTo == null) return $"Hero '{args[2]}' has no party.";
        if (!objectManager.TryGetIdWithLogging(alley, out var alleyId) ||
            !objectManager.TryGetIdWithLogging(owner, out var ownerId) ||
            !objectManager.TryGetIdWithLogging(owner.CharacterObject, out var ownerCharacterId))
            return "Unable to resolve the alley recruit fixture ids.";

        sessionInterface.TryGetManagementData(alleyId, out var originalManagementData);
        fixture = new AlleyRecruitFixture(
            alley,
            alleyId,
            alley.Owner,
            CloneManagementData(originalManagementData),
            owner.PartyBelongedTo,
            owner.PartyBelongedTo.MemberRoster.GetTroopRoster().ToArray());

        try
        {
            alley.SetOwner(owner);
            sessionInterface.SetManagementData(
                alleyId,
                ownerId,
                new[] { new TroopRosterElementData(ownerCharacterId, 1, 0, 0) });
            sessionInterface.ClearUnderAttackByAi(alleyId);
            sessionInterface.SetLastRecruitTimeTicks(alleyId, 0);
            if (!sessionInterface.TryGetManagementData(alleyId, out var managementData))
                throw new InvalidOperationException("The fixture management data was not stored.");

            network.SendAll(new NetworkAlleyManagementUpdated(
                alleyId,
                managementData.OverseerId,
                managementData.Garrison,
                managementData.LastRecruitTimeTicks));
            network.SendAll(new NetworkAlleyUnderAttack(alleyId, null, default, showNotification: false));

            return $"ALLEY_RECRUIT_FIXTURE_STARTED settlement={args[0]} alley={args[1]} " +
                   $"owner={owner.StringId} party={owner.PartyBelongedTo.StringId} " +
                   $"originalOwner={fixture.OriginalOwner?.StringId ?? "none"} " +
                   $"originalRosterEntries={fixture.MemberRoster.Length}";
        }
        catch (Exception e)
        {
            var rollback = RestoreFixture(new List<string>());
            return $"Alley recruit fixture setup failed: {e.Message}. Rollback: {rollback}";
        }
    }

    [CommandLineArgumentFunction("recruit_fixture_state", "coop.debug.alley")]
    public static string FixtureState(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.alley.recruit_fixture_state";
        if (fixture == null) return "The alley recruit fixture is not active.";

        var party = fixture.PlayerParty;
        return $"ALLEY_RECRUIT_FIXTURE_STATE alley={fixture.AlleyId} owner={fixture.Alley.Owner?.StringId ?? "none"} " +
               $"party={party.StringId} rosterEntries={party.MemberRoster.Count} totalMembers={party.MemberRoster.TotalManCount} " +
               $"mapEvent={(party.MapEvent == null ? "none" : "active")}";
    }

    [CommandLineArgumentFunction("recruit_roster", "coop.debug.alley")]
    public static string RecruitRoster(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.alley.recruit_roster <heroRegistryId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve IObjectManager.";
        if (!objectManager.TryGetObjectWithLogging<Hero>(args[0], out var hero))
            return $"Hero '{args[0]}' not found.";
        if (hero.PartyBelongedTo == null) return $"Hero '{args[0]}' has no party.";

        var roster = hero.PartyBelongedTo.MemberRoster;
        var result = new StringBuilder();
        result.AppendLine(
            $"ALLEY_RECRUIT_ROSTER hero={hero.StringId} registry={args[0]} " +
            $"party={hero.PartyBelongedTo.StringId} total={roster.TotalManCount}");
        for (var index = 0; index < roster.Count; index++)
        {
            var element = roster.GetElementCopyAtIndex(index);
            result.AppendLine(
                $"{element.Character.StringId}: number={element.Number} " +
                $"wounded={element.WoundedNumber} xp={element.Xp} hero={element.Character.IsHero}");
        }
        return result.ToString();
    }

    [CommandLineArgumentFunction("recruit_fixture_restore", "coop.debug.alley")]
    public static string RestoreFixture(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (fixture == null) return "The alley recruit fixture is not active.";
        if (fixture.PlayerParty.MapEvent != null) return "Finish the player's map event before restoring the fixture.";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<ISessionAlleyPlayerDataInterface>(out var sessionInterface) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<ISendCoalescer>(out var sendCoalescer))
            return "Unable to resolve the alley recruit fixture services.";

        try
        {
            var charactersToReset = fixture.PlayerParty.MemberRoster.GetTroopRoster()
                .Select(element => element.Character)
                .Concat(fixture.MemberRoster.Select(element => element.Character))
                .ToList();
            foreach (var characterId in args)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(characterId, out var character))
                    return $"Character '{characterId}' not found.";
                charactersToReset.Add(character);
            }

            if (!objectManager.TryGetIdWithLogging(fixture.PlayerParty.MemberRoster, out var rosterId))
                return "Unable to resolve the fixture roster id.";
            var resetCharacters = new List<(CharacterObject Character, string Id)>();
            foreach (var character in charactersToReset.Distinct())
            {
                if (!objectManager.TryGetIdWithLogging(character, out var characterId))
                    return $"Unable to resolve the fixture character id for '{character.StringId}'.";
                resetCharacters.Add((character, characterId));
            }

            RestoreRoster(fixture.PlayerParty.MemberRoster, fixture.MemberRoster);
            sendCoalescer.DropInstance(Compact(rosterId, typeof(TroopRoster)));
            foreach (var resetCharacter in resetCharacters)
            {
                var index = fixture.PlayerParty.MemberRoster.FindIndexOfTroop(resetCharacter.Character);
                var element = index >= 0
                    ? fixture.PlayerParty.MemberRoster.GetElementCopyAtIndex(index)
                    : default;
                network.SendAll(new NetworkTroopRosterSetWoundedNumber(
                    rosterId,
                    resetCharacter.Id,
                    index >= 0 ? element.WoundedNumber : 0));
                network.SendAll(new NetworkTroopRosterSetNumber(
                    rosterId,
                    resetCharacter.Id,
                    index >= 0 ? element.Number : 0));
                if (index >= 0)
                {
                    network.SendAll(new NetworkTroopRosterElementBatch(
                        rosterId,
                        resetCharacter.Id,
                        new[] { TroopRosterElementOperation.SetXp(element.Xp) }));
                }
            }
            fixture.PlayerParty.MemberRoster.RemoveZeroCounts();
            network.SendAll(new NetworkTroopRosterRemoveZeroCounts(rosterId));
            fixture.Alley.SetOwner(fixture.OriginalOwner);

            if (fixture.OriginalManagementData == null)
            {
                sessionInterface.RemoveManagementData(fixture.AlleyId);
                network.SendAll(new NetworkAlleyManagementRemoved(fixture.AlleyId));
            }
            else
            {
                var data = fixture.OriginalManagementData;
                sessionInterface.SetManagementData(fixture.AlleyId, data.OverseerId, data.Garrison);
                sessionInterface.SetLastRecruitTimeTicks(fixture.AlleyId, data.LastRecruitTimeTicks);
                if (data.UnderAttackByAlleyId != null)
                {
                    sessionInterface.SetUnderAttackByAi(
                        fixture.AlleyId,
                        data.UnderAttackByAlleyId,
                        data.AttackResponseDueDate);
                }
                else
                {
                    sessionInterface.ClearUnderAttackByAi(fixture.AlleyId);
                }
                if (!sessionInterface.TryGetManagementData(fixture.AlleyId, out var restored))
                    throw new InvalidOperationException("The original alley management data was not restored.");

                network.SendAll(new NetworkAlleyManagementUpdated(
                    fixture.AlleyId,
                    restored.OverseerId,
                    restored.Garrison,
                    restored.LastRecruitTimeTicks));
                network.SendAll(new NetworkAlleyUnderAttack(
                    fixture.AlleyId,
                    restored.UnderAttackByAlleyId,
                    restored.AttackResponseDueDate,
                    showNotification: false));
            }

            var restoredTotal = fixture.PlayerParty.MemberRoster.TotalManCount;
            fixture = null;
            return $"ALLEY_RECRUIT_FIXTURE_RESTORED totalMembers={restoredTotal}";
        }
        catch (Exception e)
        {
            return $"Alley recruit fixture restore failed: {e.Message}";
        }
    }

    [CommandLineArgumentFunction("recruit_overseer_state", "coop.debug.alley")]
    public static string RecruitOverseerState(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the owning client.";
        if (args.Count != 2)
            return "Usage: coop.debug.alley.recruit_overseer_state <settlementId> <alleyIndex>";
        if (!TryGetAlley(args[0], args[1], out var alley, out var error)) return error;
        if (Mission.Current == null) return "ALLEY_RECRUIT_OVERSEER_STATE mission=False present=False";

        var behavior = Campaign.Current?.GetCampaignBehavior<AlleyCampaignBehavior>();
        var playerAlleyData = behavior?._playerOwnedCommonAreaData.FirstOrDefault(data => data.Alley == alley);
        if (playerAlleyData == null) return "ALLEY_RECRUIT_OVERSEER_STATE mission=True present=False owner=False";

        var overseerAgent = Mission.Current.Agents.FirstOrDefault(candidate =>
            candidate.Character is CharacterObject character &&
            character.HeroObject == playerAlleyData.AssignedClanMember);
        return $"ALLEY_RECRUIT_OVERSEER_STATE mission=True present={overseerAgent != null} " +
               $"owner=True overseer={playerAlleyData.AssignedClanMember.StringId}";
    }

    [CommandLineArgumentFunction("recruit_conversation_start", "coop.debug.alley")]
    public static string StartRecruitConversation(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the owning client.";
        if (args.Count != 2)
            return "Usage: coop.debug.alley.recruit_conversation_start <settlementId> <alleyIndex>";
        if (!TryGetAlley(args[0], args[1], out var alley, out var error)) return error;
        if (Mission.Current == null) return "Enter the alley location before starting the conversation.";

        var behavior = Campaign.Current?.GetCampaignBehavior<AlleyCampaignBehavior>();
        var playerAlleyData = behavior?._playerOwnedCommonAreaData.FirstOrDefault(data => data.Alley == alley);
        if (playerAlleyData == null) return "The local player does not own this alley.";

        var overseerAgent = Mission.Current.Agents.FirstOrDefault(candidate =>
            candidate.Character is CharacterObject character &&
            character.HeroObject == playerAlleyData.AssignedClanMember);
        if (overseerAgent == null) return "The assigned alley overseer is not present in the mission.";

        var conversation = Mission.Current.GetMissionBehavior<MissionConversationLogic>();
        if (conversation == null) return "The mission conversation behavior is unavailable.";
        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            return "A conversation is already in progress.";

        conversation.StartConversation(overseerAgent, setActionsInstantly: false);
        return $"ALLEY_RECRUIT_CONVERSATION_STARTED overseer={playerAlleyData.AssignedClanMember.StringId}";
    }

    [CommandLineArgumentFunction("recruit_conversation", "coop.debug.alley")]
    public static string RecruitConversation(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the owning client.";
        if (args.Count != 1) return "Usage: coop.debug.alley.recruit_conversation <state|ask|accept>";

        var manager = Campaign.Current?.ConversationManager;
        if (manager?.IsConversationInProgress != true) return "No alley conversation is active.";

        switch (args[0].ToLowerInvariant())
        {
            case "state":
                return FormatConversationState(manager);
            case "ask":
                if (!manager.CurOptions.Any(option => option.Id == AskForVolunteersOptionId))
                    return $"Conversation option '{AskForVolunteersOptionId}' is not available. {FormatConversationState(manager)}";
                manager.DoOption(AskForVolunteersOptionId);
                return $"ALLEY_RECRUIT_ASKED {FormatConversationState(manager)}";
            case "accept":
                if (!manager.CurOptions.Any(option => option.Id == AcceptVolunteersOptionId))
                    return $"Conversation option '{AcceptVolunteersOptionId}' is not available. {FormatConversationState(manager)}";
                var offer = FormatCurrentOffer();
                manager.DoOption(AcceptVolunteersOptionId);
                return $"ALLEY_RECRUIT_ACCEPTED offer={offer} {FormatConversationState(manager)}";
            default:
                return "Usage: coop.debug.alley.recruit_conversation <state|ask|accept>";
        }
    }

    [CommandLineArgumentFunction("recruit_inventory", "coop.debug.alley")]
    public static string RecruitInventory(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the owning client.";
        if (args.Count == 0)
            return "Usage: coop.debug.alley.recruit_inventory <open|trade|purchase|complete|state> [itemId]";
        if (!args[0].Equals("purchase", StringComparison.OrdinalIgnoreCase) && args.Count != 1)
            return "Usage: coop.debug.alley.recruit_inventory <open|trade|purchase|complete|state> [itemId]";

        switch (args[0].ToLowerInvariant())
        {
            case "open":
                InventoryScreenHelper.OpenScreenAsInventory();
                RepairLocalPlayerInventoryContext();
                return "ALLEY_RECRUIT_INVENTORY_OPENED";
            case "trade":
                if (Settlement.CurrentSettlement?.StringId != "town_ES1")
                    return "Enter Danustica (town_ES1) before opening its trade screen.";
                InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
                RepairLocalPlayerInventoryContext();
                return "ALLEY_RECRUIT_TRADE_OPENED settlement=town_ES1";
            case "purchase":
                if (args.Count != 2)
                    return "Usage: coop.debug.alley.recruit_inventory purchase <itemId>";
                return StageDanusticaPurchase(args[1]);
            case "complete":
                if (!(GameStateManager.Current?.ActiveState is InventoryState))
                    return "No inventory or trade screen is active.";
                InventoryScreenHelper.CloseScreen(fromCancel: false);
                return GameStateManager.Current?.ActiveState is InventoryState
                    ? "ALLEY_RECRUIT_INVENTORY_COMPLETE_REJECTED"
                    : "ALLEY_RECRUIT_INVENTORY_COMPLETED";
            case "state":
                var state = GameStateManager.Current?.ActiveState;
                var inventoryState = state as InventoryState;
                return $"ALLEY_RECRUIT_SCREEN_STATE active={state?.GetType().Name ?? "none"} " +
                       $"mode={inventoryState?.InventoryMode.ToString() ?? "none"} " +
                       $"settlement={Settlement.CurrentSettlement?.StringId ?? "none"}";
            default:
                return "Usage: coop.debug.alley.recruit_inventory <open|trade|purchase|complete|state> [itemId]";
        }
    }

    [CommandLineArgumentFunction("recruit_start_looter_battle", "coop.debug.alley")]
    public static string StartLooterBattle(List<string> args)
    {
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.alley.recruit_start_looter_battle <heroRegistryId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve IObjectManager.";
        if (!objectManager.TryGetObjectWithLogging<Hero>(args[0], out var owner))
            return $"Hero '{args[0]}' not found.";
        var playerParty = owner.PartyBelongedTo;
        if (playerParty == null || playerParty.MapEvent != null)
            return "The fixture owner must lead a party outside a map event.";

        if (playerParty.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(playerParty);
        var position = playerParty.Position.ToVec2();
        var banditParty = MobileParty.All
            .Where(candidate => candidate.IsActive && candidate.IsBandit && candidate != playerParty &&
                candidate.MapEvent == null && candidate.CurrentSettlement == null &&
                candidate.MemberRoster.TotalManCount > 0)
            .OrderBy(candidate => candidate.Position.ToVec2().DistanceSquared(position))
            .FirstOrDefault();
        if (banditParty == null) return "No active bandit party is available for the loot fixture.";

        StartBattleAction.Apply(banditParty.Party, playerParty.Party);
        if (playerParty.MapEvent == null) return "The looter battle map event was not created.";
        return $"ALLEY_RECRUIT_LOOTER_BATTLE_STARTED party={banditParty.StringId} " +
               $"troops={banditParty.MemberRoster.TotalManCount}";
    }

    private static void RepairLocalPlayerInventoryContext()
    {
        var character = Hero.MainHero?.CharacterObject;
        var inventoryLogic = InventoryScreenHelper.GetActiveInventoryState()?.InventoryLogic;
        if (character == null || inventoryLogic == null)
            throw new InvalidOperationException("The local player inventory context is unavailable.");

        inventoryLogic.OwnerCharacter = character;
        inventoryLogic.InitialEquipmentCharacter = character;
    }

    private static string StageDanusticaPurchase(string itemId)
    {
        var inventoryState = GameStateManager.Current?.ActiveState as InventoryState;
        var inventoryLogic = inventoryState?.InventoryLogic;
        if (inventoryState?.InventoryMode != InventoryScreenHelper.InventoryMode.Trade || inventoryLogic == null)
            return "Open the Danustica trade screen before staging a purchase.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<ItemObject>(itemId, out var item))
            return $"Unable to resolve item '{itemId}'.";

        var element = inventoryLogic
            .GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)
            .FirstOrDefault(candidate =>
                candidate.EquipmentElement.Item == item &&
                candidate.EquipmentElement.ItemModifier == null &&
                candidate.Amount > 0);
        if (element.EquipmentElement.Item == null)
            return $"The Danustica market does not contain unmodified item '{itemId}'.";

        var price = inventoryLogic.GetCostOfItemRosterElement(
            element,
            InventoryLogic.InventorySide.OtherInventory);
        if (price <= 0 || Hero.MainHero.Gold < inventoryLogic.TotalAmount + price)
            return $"The Danustica purchase is not affordable: item={itemId} price={price}.";

        var previousDebt = inventoryLogic.TotalAmount;
        inventoryLogic.AddTransferCommand(TransferCommand.Transfer(
            1,
            InventoryLogic.InventorySide.OtherInventory,
            InventoryLogic.InventorySide.PlayerInventory,
            element,
            EquipmentIndex.None,
            EquipmentIndex.None,
            null));
        if (inventoryLogic.TotalAmount != previousDebt + price)
            return $"The Danustica purchase was not staged: item={itemId} price={price}.";

        return $"ALLEY_RECRUIT_TRADE_PURCHASE_STAGED item={itemId} price={price} debt={inventoryLogic.TotalAmount}";
    }

    private static bool TryGetAlley(string settlementId, string indexArg, out Alley alley, out string error)
    {
        alley = null;
        error = null;
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
        {
            error = $"Settlement with id '{settlementId}' not found.";
            return false;
        }
        if (!int.TryParse(indexArg, out var index) ||
            settlement.Alleys == null ||
            index < 0 ||
            index >= settlement.Alleys.Count)
        {
            error = "The alley index is invalid.";
            return false;
        }

        alley = settlement.Alleys[index];
        return true;
    }

    private static string FormatConversationState(ConversationManager manager)
    {
        var options = manager.CurOptions == null
            ? "none"
            : string.Join(",", manager.CurOptions.Select(option => option.Id));
        return $"sentence={manager.CurrentSentenceText} options={options}";
    }

    private static string FormatCurrentOffer()
    {
        var behavior = Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>();
        var data = behavior._playerOwnedCommonAreaData.First(
            item => item.Alley?.Settlement == Settlement.CurrentSettlement);
        var troops = Campaign.Current.Models.AlleyModel.GetTroopsToRecruitFromAlleyDependingOnAlleyRandom(
            data.Alley,
            data.RandomFloatWeekly);
        return string.Join(",", troops.GetTroopRoster().Select(
            element => $"{element.Character.StringId}:{element.Number}"));
    }

    private static AlleyManagementData CloneManagementData(AlleyManagementData data)
    {
        if (data == null) return null;
        return new AlleyManagementData(data.OverseerId, data.Garrison?.ToArray() ?? Array.Empty<TroopRosterElementData>())
        {
            UnderAttackByAlleyId = data.UnderAttackByAlleyId,
            AttackResponseDueDate = data.AttackResponseDueDate,
            LastRecruitTimeTicks = data.LastRecruitTimeTicks,
        };
    }

    internal static bool IsFixtureAlley(string alleyId)
    {
        return fixture?.AlleyId == alleyId;
    }

    private static void RestoreRoster(TroopRoster roster, TroopRosterElement[] elements)
    {
        for (var index = roster.Count - 1; index >= 0; index--)
        {
            var element = roster.GetElementCopyAtIndex(index);
            roster.AddToCountsAtIndex(index, -element.Number, -element.WoundedNumber, -element.Xp, false);
        }
        for (var index = 0; index < elements.Length; index++)
        {
            var element = elements[index];
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true,
                index);
        }
    }

    private sealed class AlleyRecruitFixture
    {
        public Alley Alley { get; }
        public string AlleyId { get; }
        public Hero OriginalOwner { get; }
        public AlleyManagementData OriginalManagementData { get; }
        public MobileParty PlayerParty { get; }
        public TroopRosterElement[] MemberRoster { get; }

        public AlleyRecruitFixture(
            Alley alley,
            string alleyId,
            Hero originalOwner,
            AlleyManagementData originalManagementData,
            MobileParty playerParty,
            TroopRosterElement[] memberRoster)
        {
            Alley = alley;
            AlleyId = alleyId;
            OriginalOwner = originalOwner;
            OriginalManagementData = originalManagementData;
            PlayerParty = playerParty;
            MemberRoster = memberRoster;
        }
    }
}
