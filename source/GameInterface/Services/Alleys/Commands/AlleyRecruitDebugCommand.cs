using Common.Commands;
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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Alleys.Commands;

/// <summary>DEBUG commands for the exact alley-recruitment inventory, trade, and loot regression.</summary>
public class AlleyRecruitDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private const string AskForVolunteersOptionId = "alley_talk_start_player_owned_alley_manager_answer_2";
    private const string AcceptVolunteersOptionId = "alley_talk_start_player_owned_alley_manager_volunteers_3";

    private static AlleyRecruitFixture fixture;

    public sealed class AlleyRecruitFixtureStartCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_fixture_start";

        public string Description => "Starts the alley recruitment fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
            new ExpectedArgs("hero_registry_id", "The registered owner hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run this command on the server.");
            if (fixture != null) return Failed("The alley recruit fixture is already active.");

            if (!TryGetAlley(args[0], args[1], out var alley, out var error)) return Failed(error);
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
                !ContainerProvider.TryResolve<ISessionAlleyPlayerDataInterface>(out var sessionInterface) ||
                !ContainerProvider.TryResolve<INetwork>(out var network))
                return Failed("Unable to resolve the alley recruit fixture services.");
            if (!objectManager.TryGetObjectWithLogging<Hero>(args[2], out var owner)) return Failed($"Hero '{args[2]}' not found.");
            if (owner.PartyBelongedTo == null) return Failed($"Hero '{args[2]}' has no party.");
            if (!objectManager.TryGetIdWithLogging(alley, out var alleyId) ||
                !objectManager.TryGetIdWithLogging(owner, out var ownerId) ||
                !objectManager.TryGetIdWithLogging(owner.CharacterObject, out var ownerCharacterId))
                return Failed("Unable to resolve the alley recruit fixture ids.");

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

                return Succeeded($"ALLEY_RECRUIT_FIXTURE_STARTED settlement={args[0]} alley={args[1]} " +
                       $"owner={owner.StringId} party={owner.PartyBelongedTo.StringId} " +
                       $"originalOwner={fixture.OriginalOwner?.StringId ?? "none"} " +
                       $"originalRosterEntries={fixture.MemberRoster.Length}");
            }
            catch (Exception e)
            {
                CoopCommandResult rollback = RollbackFixtureSetup();
                return Failed($"Alley recruit fixture setup failed: {e.Message}. Rollback: {rollback.Output}");
            }
        }
    }

    public sealed class AlleyRecruitFixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_fixture_state";

        public string Description => "Reports alley recruitment fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (fixture == null) return Failed("The alley recruit fixture is not active.");

            var party = fixture.PlayerParty;
            return Succeeded($"ALLEY_RECRUIT_FIXTURE_STATE alley={fixture.AlleyId} owner={fixture.Alley.Owner?.StringId ?? "none"} " +
                   $"party={party.StringId} rosterEntries={party.MemberRoster.Count} totalMembers={party.MemberRoster.TotalManCount} " +
                   $"mapEvent={(party.MapEvent == null ? "none" : "active")}");
        }
    }

    public sealed class AlleyRecruitRosterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_roster";

        public string Description => "Reports the recruit roster for a hero party.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_registry_id", "The registered hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve IObjectManager.");
            if (!objectManager.TryGetObjectWithLogging<Hero>(args[0], out var hero))
                return Failed($"Hero '{args[0]}' not found.");
            if (hero.PartyBelongedTo == null) return Failed($"Hero '{args[0]}' has no party.");

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
            return Succeeded(result.ToString());
        }
    }

    public sealed class AlleyRecruitFixtureRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_fixture_restore";

        public string Description => "Restores the alley recruitment fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("character_id", "An optional extra character id to reset.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run this command on the server.");
            if (fixture == null) return Failed("The alley recruit fixture is not active.");
            if (fixture.PlayerParty.MapEvent != null) return Failed("Finish the player's map event before restoring the fixture.");

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
                !ContainerProvider.TryResolve<ISessionAlleyPlayerDataInterface>(out var sessionInterface) ||
                !ContainerProvider.TryResolve<INetwork>(out var network) ||
                !ContainerProvider.TryResolve<ISendCoalescer>(out var sendCoalescer))
                return Failed("Unable to resolve the alley recruit fixture services.");

            try
            {
                var charactersToReset = fixture.PlayerParty.MemberRoster.GetTroopRoster()
                    .Select(element => element.Character)
                    .Concat(fixture.MemberRoster.Select(element => element.Character))
                    .ToList();
                foreach (var characterId in args)
                {
                    if (!objectManager.TryGetObjectWithLogging<CharacterObject>(characterId, out var character))
                        return Failed($"Character '{characterId}' not found.");
                    charactersToReset.Add(character);
                }

                if (!objectManager.TryGetIdWithLogging(fixture.PlayerParty.MemberRoster, out var rosterId))
                    return Failed("Unable to resolve the fixture roster id.");
                var resetCharacters = new List<(CharacterObject Character, string Id)>();
                foreach (var character in charactersToReset.Distinct())
                {
                    if (!objectManager.TryGetIdWithLogging(character, out var characterId))
                        return Failed($"Unable to resolve the fixture character id for '{character.StringId}'.");
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
                return Succeeded($"ALLEY_RECRUIT_FIXTURE_RESTORED totalMembers={restoredTotal}");
            }
            catch (Exception e)
            {
                return Failed($"Alley recruit fixture restore failed: {e.Message}");
            }
        }
    }

    private static CoopCommandResult RollbackFixtureSetup()
    {
        if (ModInformation.IsClient) return Failed("Run this command on the server.");
        if (fixture == null) return Failed("The alley recruit fixture is not active.");
        if (fixture.PlayerParty.MapEvent != null) return Failed("Finish the player's map event before restoring the fixture.");

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<ISessionAlleyPlayerDataInterface>(out var sessionInterface) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<ISendCoalescer>(out var sendCoalescer))
            return Failed("Unable to resolve the alley recruit fixture services.");

        try
        {
            var charactersToReset = fixture.PlayerParty.MemberRoster.GetTroopRoster()
                .Select(element => element.Character)
                .Concat(fixture.MemberRoster.Select(element => element.Character))
                .ToList();
            if (!objectManager.TryGetIdWithLogging(fixture.PlayerParty.MemberRoster, out var rosterId))
                return Failed("Unable to resolve the fixture roster id.");
            var resetCharacters = new List<(CharacterObject Character, string Id)>();
            foreach (var character in charactersToReset.Distinct())
            {
                if (!objectManager.TryGetIdWithLogging(character, out var characterId))
                    return Failed($"Unable to resolve the fixture character id for '{character.StringId}'.");
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
            return Succeeded($"ALLEY_RECRUIT_FIXTURE_RESTORED totalMembers={restoredTotal}");
        }
        catch (Exception e)
        {
            return Failed($"Alley recruit fixture restore failed: {e.Message}");
        }

    }

    public sealed class AlleyRecruitOverseerStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_overseer_state";

        public string Description => "Reports the alley overseer mission state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on the owning client.");
            if (!TryGetAlley(args[0], args[1], out var alley, out var error)) return Failed(error);
            if (Mission.Current == null) return Succeeded("ALLEY_RECRUIT_OVERSEER_STATE mission=False present=False");

            var behavior = Campaign.Current?.GetCampaignBehavior<AlleyCampaignBehavior>();
            var playerAlleyData = behavior?._playerOwnedCommonAreaData.FirstOrDefault(data => data.Alley == alley);
            if (playerAlleyData == null) return Succeeded("ALLEY_RECRUIT_OVERSEER_STATE mission=True present=False owner=False");

            var overseerAgent = Mission.Current.Agents.FirstOrDefault(candidate =>
                candidate.Character is CharacterObject character &&
                character.HeroObject == playerAlleyData.AssignedClanMember);
            return Succeeded($"ALLEY_RECRUIT_OVERSEER_STATE mission=True present={overseerAgent != null} " +
                   $"owner=True overseer={playerAlleyData.AssignedClanMember.StringId}");
        }
    }

    public sealed class AlleyRecruitConversationStartCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_conversation_start";

        public string Description => "Starts a conversation with the alley overseer.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on the owning client.");
            if (!TryGetAlley(args[0], args[1], out var alley, out var error)) return Failed(error);
            if (Mission.Current == null) return Failed("Enter the alley location before starting the conversation.");

            var behavior = Campaign.Current?.GetCampaignBehavior<AlleyCampaignBehavior>();
            var playerAlleyData = behavior?._playerOwnedCommonAreaData.FirstOrDefault(data => data.Alley == alley);
            if (playerAlleyData == null) return Failed("The local player does not own this alley.");

            var overseerAgent = Mission.Current.Agents.FirstOrDefault(candidate =>
                candidate.Character is CharacterObject character &&
                character.HeroObject == playerAlleyData.AssignedClanMember);
            if (overseerAgent == null) return Failed("The assigned alley overseer is not present in the mission.");

            var conversation = Mission.Current.GetMissionBehavior<MissionConversationLogic>();
            if (conversation == null) return Failed("The mission conversation behavior is unavailable.");
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                return Failed("A conversation is already in progress.");

            conversation.StartConversation(overseerAgent, setActionsInstantly: false);
            return Succeeded($"ALLEY_RECRUIT_CONVERSATION_STARTED overseer={playerAlleyData.AssignedClanMember.StringId}");
        }
    }

    public sealed class AlleyRecruitConversationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_conversation";

        public string Description => "Drives or inspects the alley recruitment conversation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("action", "One of state, ask, or accept."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on the owning client.");

            var manager = Campaign.Current?.ConversationManager;
            if (manager?.IsConversationInProgress != true) return Failed("No alley conversation is active.");

            switch (args[0].ToLowerInvariant())
            {
                case "state":
                    return Succeeded(FormatConversationState(manager));
                case "ask":
                    if (!manager.CurOptions.Any(option => option.Id == AskForVolunteersOptionId))
                        return Failed($"Conversation option '{AskForVolunteersOptionId}' is not available. {FormatConversationState(manager)}");
                    manager.DoOption(AskForVolunteersOptionId);
                    return Succeeded($"ALLEY_RECRUIT_ASKED {FormatConversationState(manager)}");
                case "accept":
                    if (!manager.CurOptions.Any(option => option.Id == AcceptVolunteersOptionId))
                        return Failed($"Conversation option '{AcceptVolunteersOptionId}' is not available. {FormatConversationState(manager)}");
                    var offer = FormatCurrentOffer();
                    manager.DoOption(AcceptVolunteersOptionId);
                    return Succeeded($"ALLEY_RECRUIT_ACCEPTED offer={offer} {FormatConversationState(manager)}");
                default:
                    return Failed($"Unknown recruit conversation action: {args[0]}.");
            }
        }
    }

    public sealed class AlleyRecruitInventoryCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_inventory";

        public string Description => "Drives or inspects the alley recruitment inventory screen.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("action", "One of open, trade, complete, or state."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on the owning client.");

            switch (args[0].ToLowerInvariant())
            {
                case "open":
                    InventoryScreenHelper.OpenScreenAsInventory();
                    RepairLocalPlayerInventoryContext();
                    return Succeeded("ALLEY_RECRUIT_INVENTORY_OPENED");
                case "trade":
                    if (Settlement.CurrentSettlement?.StringId != "town_ES1")
                        return Failed("Enter Danustica (town_ES1) before opening its trade screen.");
                    InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
                    RepairLocalPlayerInventoryContext();
                    return Succeeded("ALLEY_RECRUIT_TRADE_OPENED settlement=town_ES1");
                case "complete":
                    if (!(GameStateManager.Current?.ActiveState is InventoryState))
                        return Failed("No inventory or trade screen is active.");
                    InventoryScreenHelper.CloseScreen(fromCancel: false);
                    return GameStateManager.Current?.ActiveState is InventoryState
                        ? Failed("ALLEY_RECRUIT_INVENTORY_COMPLETE_REJECTED")
                        : Succeeded("ALLEY_RECRUIT_INVENTORY_COMPLETED");
                case "state":
                    var state = GameStateManager.Current?.ActiveState;
                    var inventoryState = state as InventoryState;
                    return Succeeded($"ALLEY_RECRUIT_SCREEN_STATE active={state?.GetType().Name ?? "none"} " +
                           $"mode={inventoryState?.InventoryMode.ToString() ?? "none"} " +
                           $"settlement={Settlement.CurrentSettlement?.StringId ?? "none"}");
                default:
                    return Failed($"Unknown recruit inventory action: {args[0]}.");
            }
        }
    }

    public sealed class AlleyRecruitStartLooterBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "recruit_start_looter_battle";

        public string Description => "Starts the alley recruitment looter battle fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_registry_id", "The registered fixture-owner hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run this command on the server.");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve IObjectManager.");
            if (!objectManager.TryGetObjectWithLogging<Hero>(args[0], out var owner))
                return Failed($"Hero '{args[0]}' not found.");
            var playerParty = owner.PartyBelongedTo;
            if (playerParty == null || playerParty.MapEvent != null)
                return Failed("The fixture owner must lead a party outside a map event.");

            if (playerParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(playerParty);
            var position = playerParty.Position.ToVec2();
            var banditParty = MobileParty.All
                .Where(candidate => candidate.IsActive && candidate.IsBandit && candidate != playerParty &&
                    candidate.MapEvent == null && candidate.CurrentSettlement == null &&
                    candidate.MemberRoster.TotalManCount > 0)
                .OrderBy(candidate => candidate.Position.ToVec2().DistanceSquared(position))
                .FirstOrDefault();
            if (banditParty == null) return Failed("No active bandit party is available for the loot fixture.");

            StartBattleAction.Apply(banditParty.Party, playerParty.Party);
            if (playerParty.MapEvent == null) return Failed("The looter battle map event was not created.");
            return Succeeded($"ALLEY_RECRUIT_LOOTER_BATTLE_STARTED party={banditParty.StringId} " +
                   $"troops={banditParty.MemberRoster.TotalManCount}");
        }
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
