using Autofac;
using Common;
using Common.Messaging;
using GameInterface.CoopSessionData;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.ItemRosters.Commands
{
    internal class ItemRosterDebugCommands
    {
#if DEBUG
        private static HolisticRosterFixture holisticRosterFixture;

        private sealed class HolisticRosterFixture
        {
            public string ControllerId;
            public string ItemId;
            public ItemObject Item;
            public MobileParty PlayerParty;
            public Hero PlayerHero;
            public string PlayerHeroId;
            public Settlement Settlement;
            public string SettlementComponentId;
            public int OriginalPlayerGold;
            public int OriginalSettlementGold;
            public int TradePrice;
            public Settlement OriginalLastVisitedSettlement;
            public PartyBehaviorUpdateData OriginalPlayerBehavior;
            public List<HolisticRosterTarget> Targets;
            public bool Mutated;
            public bool TradeApplied;
            public bool EnteredSettlement;
            public bool Restored;
        }

        private sealed class HolisticRosterTarget
        {
            public string Role;
            public string OwnerId;
            public string RosterId;
            public ItemRoster Roster;
            public int OriginalAmount;
            public int Delta;
        }

        private sealed class HolisticRosterState
        {
            public string ControllerId { get; set; }
            public string ItemId { get; set; }
            public string PlayerHeroId { get; set; }
            public string SettlementId { get; set; }
            public string SettlementComponentId { get; set; }
            public int ExpectedPlayerGold { get; set; }
            public int ExpectedSettlementGold { get; set; }
            public bool ExpectedTradeHistoryPresent { get; set; }
            public float ExpectedTradeAveragePrice { get; set; }
            public int ExpectedTradeItemCount { get; set; }
            public string ExpectedCurrentSettlementId { get; set; }
            public string ExpectedLastVisitedSettlementId { get; set; }
            public HolisticRosterTargetState[] Targets { get; set; }
        }

        private sealed class HolisticRosterTargetState
        {
            public string Role { get; set; }
            public string OwnerId { get; set; }
            public string RosterId { get; set; }
            public int ExpectedAmount { get; set; }
            public int Delta { get; set; }
        }
#endif

#if DEBUG
        [CommandLineArgumentFunction("holistic_capture", "coop.debug.itemrosters")]
        public static string CaptureHolisticFixture(List<string> args)
        {
            if (ModInformation.IsClient) return "Run this command on the server.";
            if (args.Count != 2)
                return "Usage: coop.debug.itemrosters.holistic_capture <controllerId> <settlementId>";
            if (holisticRosterFixture != null && !holisticRosterFixture.Restored)
                return JsonResult(new { ok = false, error = "A holistic ItemRoster fixture is already active." });
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider) ||
                !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            {
                return JsonResult(new { ok = false, error = "Unable to resolve the fixture services." });
            }
            if (!playerManager.TryGetPlayer(args[0], out var player) ||
                !playerManager.IsConnected(player) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty) ||
                playerParty.LeaderHero == null)
            {
                return JsonResult(new { ok = false, error = $"Unable to resolve connected player {args[0]}." });
            }
            if (playerParty.MapEvent != null || playerParty.CurrentSettlement != null)
                return JsonResult(new { ok = false, error = "The player party must be on the campaign map outside a settlement." });
            if (playerParty.IsCurrentlyAtSea)
                return JsonResult(new { ok = false, error = "The player party must be on land for the Danustica trade fixture." });

            var settlement = Settlement.Find(args[1]);
            if (settlement?.Town == null || settlement.ItemRoster == null)
                return JsonResult(new { ok = false, error = $"Town settlement {args[1]} was not found." });
            if (!objectManager.TryGetId(playerParty.LeaderHero, out var playerHeroId) ||
                coopSessionProvider.CoopSession?.TradePlayerData?.PlayerItemsTradeData == null ||
                !coopSessionProvider.CoopSession.TradePlayerData.PlayerItemsTradeData.TryGetValue(
                    playerHeroId,
                    out var playerTradeHistory))
            {
                return JsonResult(new { ok = false, error = "Unable to capture the player's server trade history." });
            }

            var lordParty = MobileParty.All
                .Where(party => party.IsActive && party.IsLordParty && !party.IsPlayerParty() &&
                                party.Party?.ItemRoster != null)
                .OrderBy(party => party.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            var caravanParty = MobileParty.All
                .Where(party => party.IsActive && party.IsCaravan && !party.IsPlayerParty() &&
                                party.Party?.ItemRoster != null)
                .OrderBy(party => party.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            var banditParty = MobileParty.All
                .Where(party => party.IsActive && party.IsBandit && party.Party?.ItemRoster != null)
                .OrderBy(party => party.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (lordParty == null || caravanParty == null || banditParty == null)
                return JsonResult(new { ok = false, error = "The save needs one active AI lord party, caravan, and bandit party." });

            var itemElement = settlement.ItemRoster
                .Where(element => element.EquipmentElement.Item?.IsTradeGood == true &&
                                  element.EquipmentElement.ItemModifier == null &&
                                  element.Amount >= 2 &&
                                  element.EquipmentElement.Item.Value > 0 &&
                                  element.EquipmentElement.Item.Value <= playerParty.LeaderHero.Gold &&
                                  objectManager.TryGetId(element.EquipmentElement.Item, out var candidateItemId) &&
                                  !playerTradeHistory.ContainsKey(candidateItemId))
                .OrderBy(element => element.EquipmentElement.Item.Value)
                .ThenBy(element => element.EquipmentElement.Item.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            var item = itemElement.EquipmentElement.Item;
            if (item == null)
                return JsonResult(new { ok = false, error = "Danustica has no affordable trade-good stack with at least two items." });

            if (!behaviorSnapshot.TryCreate(playerParty, out var originalBehavior) ||
                !objectManager.TryGetId(item, out var itemId) ||
                !objectManager.TryGetId(settlement.Town, out var settlementComponentId))
            {
                return JsonResult(new { ok = false, error = "Unable to capture registered fixture identities." });
            }

            var targets = new List<HolisticRosterTarget>();
            if (!TryAddTarget(objectManager, targets, "player", playerParty.StringId, playerParty.ItemRoster, item, 1) ||
                !TryAddTarget(objectManager, targets, "ai-lord", lordParty.StringId, lordParty.ItemRoster, item, 2) ||
                !TryAddTarget(objectManager, targets, "caravan", caravanParty.StringId, caravanParty.ItemRoster, item, 3) ||
                !TryAddTarget(objectManager, targets, "bandit", banditParty.StringId, banditParty.ItemRoster, item, 4) ||
                !TryAddTarget(objectManager, targets, "settlement", settlement.StringId, settlement.ItemRoster, item, 5))
            {
                return JsonResult(new { ok = false, error = "Every managed target roster must already have a registry id." });
            }

            holisticRosterFixture = new HolisticRosterFixture
            {
                ControllerId = args[0],
                ItemId = itemId,
                Item = item,
                PlayerParty = playerParty,
                PlayerHero = playerParty.LeaderHero,
                PlayerHeroId = playerHeroId,
                Settlement = settlement,
                SettlementComponentId = settlementComponentId,
                OriginalPlayerGold = playerParty.LeaderHero.Gold,
                OriginalSettlementGold = settlement.Town.Gold,
                TradePrice = 0,
                OriginalLastVisitedSettlement = playerParty.LastVisitedSettlement,
                OriginalPlayerBehavior = originalBehavior,
                Targets = targets,
            };

            return BuildFixtureReference(holisticRosterFixture, "captured");
        }

        [CommandLineArgumentFunction("holistic_mutate", "coop.debug.itemrosters")]
        public static string MutateHolisticFixture(List<string> args)
        {
            if (ModInformation.IsClient) return "Run this command on the server.";
            if (args.Count != 0) return "Usage: coop.debug.itemrosters.holistic_mutate";

            var fixture = holisticRosterFixture;
            if (fixture == null || fixture.Restored)
                return JsonResult(new { ok = false, error = "Capture the holistic fixture first." });
            if (fixture.Mutated)
                return JsonResult(new { ok = false, error = "The managed roster mutation already ran." });

            foreach (var target in fixture.Targets)
                target.Roster.AddToCounts(new EquipmentElement(fixture.Item), target.Delta);

            fixture.Mutated = true;
            return BuildFixtureReference(fixture, "managed-rosters-mutated");
        }

        [CommandLineArgumentFunction("holistic_enter_trade", "coop.debug.itemrosters")]
        public static string EnterHolisticTrade(List<string> args)
        {
            if (ModInformation.IsClient) return "Run this command on the server.";
            if (args.Count != 0) return "Usage: coop.debug.itemrosters.holistic_enter_trade";

            var fixture = holisticRosterFixture;
            if (fixture == null || !fixture.Mutated || fixture.Restored)
                return JsonResult(new { ok = false, error = "Mutate the managed rosters before entering the trade fixture." });
            if (fixture.PlayerParty.CurrentSettlement != null && fixture.PlayerParty.CurrentSettlement != fixture.Settlement)
                return JsonResult(new { ok = false, error = "The player entered another settlement during the fixture." });

            if (fixture.PlayerParty.CurrentSettlement == null)
                EnterSettlementAction.ApplyForParty(fixture.PlayerParty, fixture.Settlement);
            fixture.EnteredSettlement = fixture.PlayerParty.CurrentSettlement == fixture.Settlement;
            if (!fixture.EnteredSettlement)
                return JsonResult(new { ok = false, error = "The player did not enter Danustica." });

            return BuildFixtureReference(fixture, "entered-trade-settlement");
        }

        [CommandLineArgumentFunction("holistic_trade", "coop.debug.itemrosters")]
        public static string ObserveHolisticTrade(List<string> args)
        {
            if (ModInformation.IsClient) return "Run this command on the server.";
            if (args.Count != 0) return "Usage: coop.debug.itemrosters.holistic_trade";

            var fixture = holisticRosterFixture;
            if (fixture == null || !fixture.Mutated || !fixture.EnteredSettlement || fixture.Restored)
                return JsonResult(new { ok = false, error = "Enter Danustica with the mutated fixture before trading." });
            if (fixture.TradeApplied)
                return BuildFixtureReference(fixture, "trade-completed");

            var playerTarget = fixture.Targets.Single(target => target.Role == "player");
            var settlementTarget = fixture.Targets.Single(target => target.Role == "settlement");
            var playerAmount = playerTarget.Roster.GetItemNumber(fixture.Item);
            var settlementAmount = settlementTarget.Roster.GetItemNumber(fixture.Item);
            var playerGoldSpent = fixture.OriginalPlayerGold - fixture.PlayerHero.Gold;
            var settlementGoldGained = fixture.Settlement.Town.Gold - fixture.OriginalSettlementGold;
            if (!TryGetTradeHistory(
                    fixture.PlayerHeroId,
                    fixture.PlayerHero,
                    fixture.ItemId,
                    fixture.Item,
                    out _,
                    out var tradeHistoryPresent,
                    out var tradeAveragePrice,
                    out var tradeItemCount,
                    out var tradeHistoryError))
            {
                return JsonResult(new { ok = false, error = tradeHistoryError });
            }

            var completed = playerAmount == playerTarget.OriginalAmount + playerTarget.Delta + 1 &&
                            settlementAmount == settlementTarget.OriginalAmount + settlementTarget.Delta - 1 &&
                            playerGoldSpent > 0 &&
                            settlementGoldGained == playerGoldSpent &&
                            tradeHistoryPresent &&
                            tradeItemCount == 1 &&
                            Math.Abs(tradeAveragePrice - playerGoldSpent) < 0.001f;
            if (!completed)
            {
                return JsonResult(new
                {
                    ok = false,
                    phase = "trade-pending",
                    playerAmount,
                    settlementAmount,
                    playerGoldSpent,
                    settlementGoldGained,
                    tradeHistoryPresent,
                    tradeAveragePrice,
                    tradeItemCount,
                });
            }

            fixture.TradePrice = playerGoldSpent;
            fixture.TradeApplied = true;
            return BuildFixtureReference(fixture, "trade-completed");
        }

        [CommandLineArgumentFunction("holistic_state", "coop.debug.itemrosters")]
        public static string GetHolisticState(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: coop.debug.itemrosters.holistic_state <expectedStateJson>";
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return JsonResult(new { ok = false, error = "Unable to resolve ObjectManager." });

            HolisticRosterState expected;
            try
            {
                expected = JsonConvert.DeserializeObject<HolisticRosterState>(args[0]);
            }
            catch (JsonException exception)
            {
                return JsonResult(new { ok = false, error = exception.Message });
            }
            if (expected?.Targets == null || expected.Targets.Length != 5 ||
                !objectManager.TryGetObjectWithLogging<ItemObject>(expected.ItemId, out var item) ||
                !objectManager.TryGetObjectWithLogging<Hero>(expected.PlayerHeroId, out var playerHero) ||
                !objectManager.TryGetObjectWithLogging<SettlementComponent>(expected.SettlementComponentId, out var settlementComponent))
            {
                return JsonResult(new { ok = false, error = "Unable to resolve the expected holistic state." });
            }

            var observedTargets = new List<object>();
            var matchesExpected = true;
            foreach (var target in expected.Targets)
            {
                if (!objectManager.TryGetObjectWithLogging<ItemRoster>(target.RosterId, out var roster))
                {
                    matchesExpected = false;
                    observedTargets.Add(new { target.Role, target.OwnerId, target.RosterId, resolved = false });
                    continue;
                }

                var actualAmount = roster.GetItemNumber(item);
                var matches = actualAmount == target.ExpectedAmount;
                matchesExpected &= matches;
                observedTargets.Add(new
                {
                    target.Role,
                    target.OwnerId,
                    target.RosterId,
                    resolved = true,
                    expectedAmount = target.ExpectedAmount,
                    actualAmount,
                    matches,
                });
            }

            var playerGoldMatches = playerHero.Gold == expected.ExpectedPlayerGold;
            var settlementGoldMatches = settlementComponent.Gold == expected.ExpectedSettlementGold;
            if (!TryGetTradeHistory(
                    expected.PlayerHeroId,
                    playerHero,
                    expected.ItemId,
                    item,
                    out var tradeHistoryApplicable,
                    out var tradeHistoryPresent,
                    out var tradeAveragePrice,
                    out var tradeItemCount,
                    out var tradeHistoryError))
            {
                return JsonResult(new { ok = false, error = tradeHistoryError });
            }
            var tradeHistoryMatches = !tradeHistoryApplicable ||
                                      (tradeHistoryPresent == expected.ExpectedTradeHistoryPresent &&
                                       (!tradeHistoryPresent ||
                                        (Math.Abs(tradeAveragePrice - expected.ExpectedTradeAveragePrice) < 0.001f &&
                                         tradeItemCount == expected.ExpectedTradeItemCount)));
            var currentSettlementId = playerHero.PartyBelongedTo?.CurrentSettlement?.StringId;
            var lastVisitedSettlementId = playerHero.PartyBelongedTo?.LastVisitedSettlement?.StringId;
            var currentSettlementMatches = currentSettlementId == expected.ExpectedCurrentSettlementId;
            var lastVisitedSettlementMatches = lastVisitedSettlementId == expected.ExpectedLastVisitedSettlementId;
            matchesExpected &= playerGoldMatches && settlementGoldMatches && tradeHistoryMatches &&
                               currentSettlementMatches && lastVisitedSettlementMatches;
            return JsonResult(new
            {
                ok = true,
                role = ModInformation.IsServer ? "server" : "client",
                matchesExpected,
                itemId = expected.ItemId,
                targets = observedTargets,
                expectedPlayerGold = expected.ExpectedPlayerGold,
                actualPlayerGold = playerHero.Gold,
                playerGoldMatches,
                expectedSettlementGold = expected.ExpectedSettlementGold,
                actualSettlementGold = settlementComponent.Gold,
                settlementGoldMatches,
                tradeHistoryApplicable,
                expectedTradeHistoryPresent = expected.ExpectedTradeHistoryPresent,
                actualTradeHistoryPresent = tradeHistoryPresent,
                expectedTradeAveragePrice = expected.ExpectedTradeAveragePrice,
                actualTradeAveragePrice = tradeAveragePrice,
                expectedTradeItemCount = expected.ExpectedTradeItemCount,
                actualTradeItemCount = tradeItemCount,
                tradeHistoryMatches,
                expectedCurrentSettlementId = expected.ExpectedCurrentSettlementId,
                currentSettlementId,
                currentSettlementMatches,
                expectedLastVisitedSettlementId = expected.ExpectedLastVisitedSettlementId,
                lastVisitedSettlementId,
                lastVisitedSettlementMatches,
            });
        }

        [CommandLineArgumentFunction("holistic_restore", "coop.debug.itemrosters")]
        public static string RestoreHolisticFixture(List<string> args)
        {
            if (ModInformation.IsClient) return "Run this command on the server.";
            if (args.Count != 1)
                return "Usage: coop.debug.itemrosters.holistic_restore <capturedStateJson>";

            var fixture = holisticRosterFixture;
            if (fixture == null)
                return JsonResult(new { ok = false, error = "No holistic fixture was captured." });
            if (fixture.Restored)
                return BuildFixtureReference(fixture, "restored");

            HolisticRosterState captured;
            try
            {
                captured = JsonConvert.DeserializeObject<HolisticRosterState>(args[0]);
            }
            catch (JsonException exception)
            {
                return JsonResult(new { ok = false, error = exception.Message });
            }
            if (!MatchesCapturedFixture(fixture, captured))
                return JsonResult(new { ok = false, error = "The captured state does not match the active holistic fixture." });

            foreach (var target in fixture.Targets)
            {
                var currentAmount = target.Roster.GetItemNumber(fixture.Item);
                target.Roster.AddToCounts(new EquipmentElement(fixture.Item), target.OriginalAmount - currentAmount);
            }

            GiveGoldAction.ApplyBetweenCharacters(
                null,
                fixture.PlayerHero,
                fixture.OriginalPlayerGold - fixture.PlayerHero.Gold,
                false);
            fixture.Settlement.Town.ChangeGold(fixture.OriginalSettlementGold - fixture.Settlement.Town.Gold);

            if (fixture.TradeApplied)
            {
                if (!ContainerProvider.TryResolve<ISessionTradePlayerDataInterface>(out var sessionTradePlayerData))
                {
                    return JsonResult(new { ok = false, error = "Unable to resolve the trade-history service." });
                }
                sessionTradePlayerData.UpdatePlayerInventory(
                    fixture.PlayerHero,
                    new List<(ItemRosterElement, int)>(),
                    new List<(ItemRosterElement, int)>
                    {
                        (new ItemRosterElement(fixture.Item, 1, null), fixture.TradePrice),
                    },
                    true);
            }

            if (fixture.PlayerParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(fixture.PlayerParty);
            fixture.PlayerParty.LastVisitedSettlement = fixture.OriginalLastVisitedSettlement;
            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            {
                return JsonResult(new { ok = false, error = "Unable to resolve the player behavior snapshot service." });
            }
            if (!behaviorSnapshot.TryApply(fixture.PlayerParty, fixture.OriginalPlayerBehavior, out var behaviorError))
            {
                return JsonResult(new { ok = false, error = "Unable to restore player behavior: " + behaviorError });
            }
            MessageBroker.Instance.Publish(
                typeof(ItemRosterDebugCommands),
                new PartyBehaviorChangeAttempted(
                    fixture.PlayerParty,
                    forcePosition: true,
                    isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea,
                    resetMovementToHold: false));

            fixture.Mutated = false;
            fixture.TradeApplied = false;
            fixture.EnteredSettlement = false;
            fixture.Restored = true;
            return BuildFixtureReference(fixture, "restored");
        }

        [CommandLineArgumentFunction("holistic_verify", "coop.debug.itemrosters")]
        public static string VerifyHolisticFixture(List<string> args)
        {
            if (ModInformation.IsClient) return "Run this command on the server.";
            if (args.Count != 0) return "Usage: coop.debug.itemrosters.holistic_verify";
            if (holisticRosterFixture == null || !holisticRosterFixture.Restored)
                return JsonResult(new { ok = false, error = "The holistic fixture has not been restored." });

            var fixture = holisticRosterFixture;
            var rostersRestored = fixture.Targets.All(target =>
                target.Roster.GetItemNumber(fixture.Item) == target.OriginalAmount);
            if (!TryGetTradeHistory(
                    fixture.PlayerHeroId,
                    fixture.PlayerHero,
                    fixture.ItemId,
                    fixture.Item,
                    out _,
                    out var tradeHistoryPresent,
                    out _,
                    out _,
                    out var tradeHistoryError))
            {
                return JsonResult(new { ok = false, error = tradeHistoryError });
            }
            var tradeHistoryRestored = !tradeHistoryPresent;
            var restored = rostersRestored &&
                           fixture.PlayerHero.Gold == fixture.OriginalPlayerGold &&
                           fixture.Settlement.Town.Gold == fixture.OriginalSettlementGold &&
                           tradeHistoryRestored &&
                           fixture.PlayerParty.CurrentSettlement == null &&
                           fixture.PlayerParty.LastVisitedSettlement == fixture.OriginalLastVisitedSettlement &&
                           fixture.PlayerParty.MapEvent == null;
            return JsonResult(new
            {
                ok = restored,
                phase = "verified",
                rostersRestored,
                playerGoldRestored = fixture.PlayerHero.Gold == fixture.OriginalPlayerGold,
                settlementGoldRestored = fixture.Settlement.Town.Gold == fixture.OriginalSettlementGold,
                tradeHistoryRestored,
                playerOutsideSettlement = fixture.PlayerParty.CurrentSettlement == null,
                lastVisitedSettlementRestored = fixture.PlayerParty.LastVisitedSettlement == fixture.OriginalLastVisitedSettlement,
                playerOutsideMapEvent = fixture.PlayerParty.MapEvent == null,
            });
        }

        private static bool TryAddTarget(
            IObjectManager objectManager,
            ICollection<HolisticRosterTarget> targets,
            string role,
            string ownerId,
            ItemRoster roster,
            ItemObject item,
            int delta)
        {
            if (roster == null || !objectManager.TryGetId(roster, out var rosterId)) return false;
            targets.Add(new HolisticRosterTarget
            {
                Role = role,
                OwnerId = ownerId,
                RosterId = rosterId,
                Roster = roster,
                OriginalAmount = roster.GetItemNumber(item),
                Delta = delta,
            });
            return true;
        }

        private static bool MatchesCapturedFixture(HolisticRosterFixture fixture, HolisticRosterState captured)
        {
            if (captured?.Targets == null || captured.Targets.Length != fixture.Targets.Count ||
                captured.ControllerId != fixture.ControllerId || captured.ItemId != fixture.ItemId ||
                captured.PlayerHeroId != fixture.PlayerHeroId ||
                captured.SettlementId != fixture.Settlement.StringId ||
                captured.SettlementComponentId != fixture.SettlementComponentId ||
                captured.ExpectedPlayerGold != fixture.OriginalPlayerGold ||
                captured.ExpectedSettlementGold != fixture.OriginalSettlementGold ||
                captured.ExpectedTradeHistoryPresent ||
                captured.ExpectedTradeAveragePrice != 0f ||
                captured.ExpectedTradeItemCount != 0 ||
                captured.ExpectedCurrentSettlementId != null ||
                captured.ExpectedLastVisitedSettlementId != fixture.OriginalLastVisitedSettlement?.StringId)
            {
                return false;
            }

            return fixture.Targets.All(target => captured.Targets.Any(capturedTarget =>
                capturedTarget.Role == target.Role &&
                capturedTarget.OwnerId == target.OwnerId &&
                capturedTarget.RosterId == target.RosterId &&
                capturedTarget.ExpectedAmount == target.OriginalAmount &&
                capturedTarget.Delta == target.Delta));
        }

        private static string BuildFixtureReference(HolisticRosterFixture fixture, string phase)
        {
            var playerTradeDelta = fixture.TradeApplied ? 1 : 0;
            var settlementTradeDelta = fixture.TradeApplied ? -1 : 0;
            var targets = fixture.Targets.Select(target => new
            {
                role = target.Role,
                ownerId = target.OwnerId,
                rosterId = target.RosterId,
                expectedAmount = target.OriginalAmount +
                                 (fixture.Mutated ? target.Delta : 0) +
                                 (target.Role == "player" ? playerTradeDelta : 0) +
                                 (target.Role == "settlement" ? settlementTradeDelta : 0),
                delta = target.Delta,
            }).ToArray();
            return JsonResult(new
            {
                ok = true,
                phase,
                controllerId = fixture.ControllerId,
                itemId = fixture.ItemId,
                itemName = fixture.Item.Name.ToString(),
                playerHeroId = fixture.PlayerHeroId,
                settlementId = fixture.Settlement.StringId,
                settlementComponentId = fixture.SettlementComponentId,
                expectedPlayerGold = fixture.OriginalPlayerGold - (fixture.TradeApplied ? fixture.TradePrice : 0),
                expectedSettlementGold = fixture.OriginalSettlementGold + (fixture.TradeApplied ? fixture.TradePrice : 0),
                expectedTradeHistoryPresent = fixture.TradeApplied,
                expectedTradeAveragePrice = fixture.TradeApplied ? fixture.TradePrice : 0f,
                expectedTradeItemCount = fixture.TradeApplied ? 1 : 0,
                expectedCurrentSettlementId = fixture.EnteredSettlement && !fixture.Restored
                    ? fixture.Settlement.StringId
                    : null,
                expectedLastVisitedSettlementId = fixture.EnteredSettlement && !fixture.Restored
                    ? fixture.Settlement.StringId
                    : fixture.OriginalLastVisitedSettlement?.StringId,
                tradePrice = fixture.TradePrice,
                targets,
            });
        }

        private static bool TryGetTradeHistory(
            string playerHeroId,
            Hero playerHero,
            string itemId,
            ItemObject item,
            out bool applicable,
            out bool present,
            out float averagePrice,
            out int itemCount,
            out string error)
        {
            applicable = ModInformation.IsServer || playerHero == Hero.MainHero;
            present = false;
            averagePrice = 0f;
            itemCount = 0;
            error = null;
            if (!applicable) return true;

            if (ModInformation.IsServer)
            {
                if (!ContainerProvider.TryResolve<ICoopSessionProvider>(out var coopSessionProvider) ||
                    coopSessionProvider.CoopSession?.TradePlayerData?.PlayerItemsTradeData == null ||
                    !coopSessionProvider.CoopSession.TradePlayerData.PlayerItemsTradeData.TryGetValue(
                        playerHeroId,
                        out var playerTradeHistory))
                {
                    error = "Unable to resolve the player's server trade history.";
                    return false;
                }

                present = playerTradeHistory.TryGetValue(itemId, out var tradeData);
                if (present)
                {
                    averagePrice = tradeData.Item1;
                    itemCount = tradeData.Item2;
                }
                return true;
            }

            if (!ContainerProvider.TryResolve<ISessionTradePlayerDataInterface>(out var sessionTradePlayerData) ||
                !sessionTradePlayerData.TryGetTradeSkillBehavior(out var tradeSkillBehavior))
            {
                error = "Unable to resolve the client's trade history.";
                return false;
            }

            present = tradeSkillBehavior.ItemsTradeData.TryGetValue(item, out var clientTradeData);
            if (present)
            {
                averagePrice = clientTradeData.AveragePrice;
                itemCount = clientTradeData.NumItemsPurchased;
            }
            return true;
        }

        private static string JsonResult(object value) =>
            "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);
#endif

        [CommandLineArgumentFunction("add_random_item", "coop.debug.itemrosters")]
        public static string AddRandomItem(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.itemrosters.add_random_item <party base id> (i.e. town_V1)";
            }

            var settlementId = args[0];
            var settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementId);

            if (settlement == null) return $"Unable to find settlement with id: {settlementId}";

            Random random = new();

            var itemEnumerable = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

            var randomItem = itemEnumerable.Skip(random.Next(itemEnumerable.Count)).First();

            settlement.ItemRoster.AddToCounts(new EquipmentElement(randomItem), 1);

            return $"Added {randomItem.Name} to {settlement.Name}'s ItemRoster";
        }

        [CommandLineArgumentFunction("add_item_burst", "coop.debug.itemrosters")]
        public static string AddItemBurst(List<string> args)
        {
            if (ModInformation.IsClient)
            {
                return "Run this on the server; it is authoritative and replicates to clients.";
            }

            if (args.Count < 2)
            {
                return "Usage: coop.debug.itemrosters.add_item_burst <settlement id> <count> (i.e. town_ES1 20)";
            }

            var settlementId = args[0];
            var settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementId);

            if (settlement == null) return $"Unable to find settlement with id: {settlementId}";

            if (!int.TryParse(args[1], out var count) || count < 1)
            {
                return $"Invalid count: '{args[1]}'. Provide a positive integer.";
            }

            var itemEnumerable = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

            if (itemEnumerable.Count == 0) return "No items are loaded.";

            Random random = new();

            var randomItem = itemEnumerable.Skip(random.Next(itemEnumerable.Count)).First();

            // Add the same item count times in one tick so the coalescer collapses them into a single
            // update carrying the final count.
            for (int i = 0; i < count; i++)
            {
                settlement.ItemRoster.AddToCounts(new EquipmentElement(randomItem), 1);
            }

            return $"Added {count}x {randomItem.Name} to {settlement.Name}'s ItemRoster in one tick";
        }

        [CommandLineArgumentFunction("info", "coop.debug.itemrosters")]
        public static string Info(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.itemrosters.info <party base id> (i.e. town_V1)";
            }

            var roster = FindItemRoster(args[0], out string owner);
            
            if (roster == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            return string.Format("ItemRoster info for '{0}':\n  Items: {1}\n  Count: {2}\n  SHA1: {3:X}\n",
                owner, roster.Count, roster.Sum((i) => { return i.Amount; }), ItemRosterHash(roster));
        }

        [CommandLineArgumentFunction("export", "coop.debug.itemrosters")]
        public static string Export(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.itemrosters.export <party base id> (i.e. town_V1)";
            }

            var roster = FindItemRoster(args[0], out string owner);

            if (roster == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            var name = "!" + (ModInformation.IsServer ? "server-itemroster-export-" : "client-itemroster-export-") + $"{owner}.txt";
            File.WriteAllText(name, ItemRosterContent(roster));

            return $"Exported '{owner}' into '{name}'.\n Check bannerlord bin directory.";
        }

        private static ItemRoster FindItemRoster(string id, out string name)
        {
            if (MBObjectManager.Instance.ContainsObject<Settlement>(id))
            {
                var obj = MBObjectManager.Instance.GetObject<Settlement>(id);
                
                name = obj.Town.Name.ToString();
                return obj.ItemRoster;
            }

            MobileParty party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(id);
            if (party != null)
            {
                name = party.Owner.Name.ToString();
                return party.ItemRoster;
            }

            name = null;
            return null;
        }

        private static string ItemRosterContent(ItemRoster roster)
        {
            StringBuilder content = new();

            var sorted = roster.ToList();
            sorted.Sort(new ItemRosterElementComparer());
            foreach (var item in sorted)
            {
                content.Append(item.EquipmentElement.Item.StringId + " ");
                if (item.EquipmentElement.ItemModifier != null)
                    content.Append(item.EquipmentElement.ItemModifier.StringId + " ");
                content.Append(item.Amount);
                content.AppendLine();
            }
            return content.ToString();
        }

        private static string ItemRosterHash(ItemRoster roster)
        {
            return HashString(ItemRosterContent(roster));
        }

        private static string HashString(string input)
        {
            using SHA1Managed sha1 = new();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

        private class ItemRosterElementComparer : IComparer<ItemRosterElement>
        {
            public int Compare(ItemRosterElement x, ItemRosterElement y)
            {
                var first = x.EquipmentElement.Item.StringId;
                if (x.EquipmentElement.ItemModifier != null)
                    first += x.EquipmentElement.ItemModifier.StringId;
                first += x.Amount;

                var second = y.EquipmentElement.Item.StringId;
                if (y.EquipmentElement.ItemModifier != null)
                    second += y.EquipmentElement.ItemModifier.StringId;
                second += y.Amount;

                return first.CompareTo(second);
            }
        }
    }
}
