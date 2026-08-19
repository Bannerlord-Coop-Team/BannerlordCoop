using Autofac;
using Common;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands
{
    public class ClanDebugCommands
    {
        /// <summary>
        /// Attempts to get the ObjectManager
        /// </summary>
        /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
        /// <returns>True if ObjectManager was resolved, otherwise False</returns>
        private static bool TryGetObjectManager(out IObjectManager objectManager)
        {
            objectManager = null;
            if (ContainerProvider.TryGetContainer(out var container) == false) return false;

            return container.TryResolve(out objectManager);
        }

        [CommandLineArgumentFunction("open", "coop.debug.clan")]
        public static string OpenClanScreen(List<string> args)
        {
            if (!ModInformation.IsClient) return "Command can only be run on a client.";
            if (args.Count != 0) return "Usage: coop.debug.clan.open";
            if (Game.Current?.GameStateManager == null) return "The game-state manager is unavailable.";
            if (Game.Current.GameStateManager.ActiveState is ClanState) return "CLAN_SCREEN_ALREADY_OPEN";
            if (Hero.MainHero == null || Hero.MainHero.IsDead)
                return "The local main hero is unavailable.";

            Game.Current.GameStateManager.PushState(
                Game.Current.GameStateManager.CreateState<ClanState>(), 0);
            return "CLAN_SCREEN_OPENED";
        }

        [CommandLineArgumentFunction("close", "coop.debug.clan")]
        public static string CloseClanScreen(List<string> args)
        {
            if (!ModInformation.IsClient) return "Command can only be run on a client.";
            if (args.Count != 0) return "Usage: coop.debug.clan.close";
            if (!(Game.Current?.GameStateManager?.ActiveState is ClanState))
                return "No active Clan screen.";

            Game.Current.GameStateManager.PopState(0);
            return "CLAN_SCREEN_CLOSED";
        }

        [CommandLineArgumentFunction("screen_state", "coop.debug.clan")]
        public static string ClanScreenState(List<string> args)
        {
            if (!ModInformation.IsClient) return "Command can only be run on a client.";
            if (args.Count != 0) return "Usage: coop.debug.clan.screen_state";

            var clanScreen = ScreenManager.TopScreen as GauntletClanScreen;
            return $"CLAN_SCREEN_STATE active={Game.Current?.GameStateManager?.ActiveState is ClanState} " +
                $"topScreen={clanScreen != null} dataSource={clanScreen?._dataSource != null} " +
                $"parties={clanScreen?._dataSource?.ClanParties?._parties?.Count ?? -1} " +
                $"partiesSelected={clanScreen?._dataSource?.IsPartiesSelected ?? false} " +
                $"mainHero={Hero.MainHero?.StringId ?? "none"}";
        }

        [CommandLineArgumentFunction("select_parties", "coop.debug.clan")]
        public static string SelectParties(List<string> args)
        {
            if (!ModInformation.IsClient) return "Command can only be run on a client.";
            if (args.Count != 0) return "Usage: coop.debug.clan.select_parties";

            var clanScreen = ScreenManager.TopScreen as GauntletClanScreen;
            if (clanScreen?._dataSource == null) return "The Clan screen is unavailable.";

            clanScreen._dataSource.SetSelectedCategory(1);
            return $"CLAN_PARTIES_SELECTED parties={clanScreen._dataSource.ClanParties?._parties?.Count ?? -1}";
        }

        [CommandLineArgumentFunction("wage_state", "coop.debug.clan")]
        public static string WageState(List<string> args)
        {
            if (!ModInformation.IsClient) return "Command can only be run on a client.";
            if (args.Count > 1) return "Usage: coop.debug.clan.wage_state [clanId]";
            if (Campaign.Current?.Models?.PartyWageModel == null) return "The party wage model is unavailable.";
            if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";

            Clan clan = Clan.PlayerClan;
            if (args.Count == 1 && !objectManager.TryGetObject(args[0], out clan))
                return $"Unable to find clan with id: {args[0]}";
            if (clan == null) return "The target clan is unavailable.";

            var parties = new List<(string Type, MobileParty Party)>();
            parties.AddRange(clan.WarPartyComponents.Select(component => ("war-party", component.MobileParty)));
            parties.AddRange(clan.Heroes
                .SelectMany(hero => hero.OwnedCaravans)
                .Select(component => ("caravan", component.MobileParty)));
            parties.AddRange(clan.Settlements
                .Where(settlement => settlement.Town != null)
                .Select(settlement => ("garrison", settlement.Town.GarrisonParty)));

            var seen = new HashSet<MobileParty>();
            var output = new StringBuilder();
            foreach (var (type, party) in parties)
            {
                if (party == null || !seen.Add(party)) continue;
                AppendWageState(output, objectManager, type, party);
            }

            return output.Length == 0 ? "No Clan-screen parties were found." : output.ToString();
        }

        private static void AppendWageState(
            StringBuilder output,
            IObjectManager objectManager,
            string type,
            MobileParty party)
        {
            var issues = new List<string>();
            var roster = party.MemberRoster;
            if (roster == null)
            {
                issues.Add("member-roster-null");
            }
            else
            {
                for (int index = 0; index < roster.Count; index++)
                {
                    var element = roster.GetElementCopyAtIndex(index);
                    CharacterObject character = element.Character;
                    if (character == null)
                    {
                        issues.Add($"roster[{index}]-character-null");
                    }
                    else if (character.IsHero && character.HeroObject == null)
                    {
                        issues.Add($"roster[{index}]-hero-object-null:{character.StringId}");
                    }
                    else if (!character.IsHero && character.Culture == null)
                    {
                        issues.Add($"roster[{index}]-culture-null:{character.StringId}");
                    }
                }
            }

            Hero leader = party.LeaderHero;
            if (leader != null && leader.Clan == null) issues.Add("leader-clan-null");
            if (leader != null && leader.CharacterObject == null) issues.Add("leader-character-null");
            if (party.IsGarrison && party.CurrentSettlement == null) issues.Add("garrison-settlement-null");
            if (party.IsGarrison && party.CurrentSettlement?.Owner == null) issues.Add("garrison-owner-null");
            if (party.IsGarrison && party.CurrentSettlement?.Owner?.Culture == null) issues.Add("garrison-owner-culture-null");
            if (party.SiegeEvent != null && party.SiegeEvent.BesiegerCamp == null) issues.Add("besieger-camp-null");
            if (party.EffectiveQuartermaster != null && party.EffectiveQuartermaster.CharacterObject == null)
                issues.Add("quartermaster-character-null");

            string partyId = objectManager.TryGetId(party, out string registeredId)
                ? registeredId
                : party.StringId;
            string wage;
            try
            {
                wage = roster == null
                    ? "not-run"
                    : Campaign.Current.Models.PartyWageModel.GetTotalWage(party, roster).ResultNumber.ToString();
            }
            catch (Exception ex)
            {
                wage = $"exception:{ex.GetType().Name}";
            }

            output.AppendLine(
                $"CLAN_WAGE_STATE type={type} party={partyId} leader={leader?.StringId ?? "none"} " +
                $"roster={roster?.Count ?? -1} wage={wage} issues={(issues.Count == 0 ? "none" : string.Join(",", issues))}");
        }

        [CommandLineArgumentFunction("refresh_burst", "coop.debug.clan")]
        public static string RefreshBurst(List<string> args)
        {
            if (!ModInformation.IsServer) return "Command can only be run on the server.";
            if (args.Count != 2 || !int.TryParse(args[1], out var count) || count < 1 || count > 500)
                return "Usage: coop.debug.clan.refresh_burst <party id> <count 1-500>";
            if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
            if (!objectManager.TryGetObject(args[0], out MobileParty _))
                return $"Party with id {args[0]} not found.";
            if (!ContainerProvider.TryResolve<INetwork>(out var network))
                return "Unable to resolve Network.";

            for (int i = 0; i < count; i++)
            {
                network.SendAll(new RefreshAfterRoleAssignment(args[0]));
            }

            return $"REFRESH_BURST_SENT party={args[0]} count={count}";
        }

        // coop.debug.clan.list
        /// <summary>
        /// Lists all the clans
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the clans</returns>
        [CommandLineArgumentFunction("list", "coop.debug.clan")]
        public static string ListClans(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Clan> clans = Campaign.Current.CampaignObjectManager.Clans.ToList();

            clans.ForEach((clan) =>
            {
                stringBuilder.AppendLine(string.Format("ID: '{0}' Name: '{1}'", clan.StringId, clan.Name));
            });

            return stringBuilder.ToString();
        }

        // coop.debug.clan.info <clanId>
        /// <summary>
        /// Reflection-dumps every field of a Clan so a server screenshot and a client screenshot can be
        /// compared field-for-field to confirm Clan field syncs still replicate.
        /// </summary>
        [CommandLineArgumentFunction("info", "coop.debug.clan")]
        public static string Info(List<string> args)
        {
            if (args.Count != 1) return "Usage: coop.debug.clan.info <clanId>";
            if (!TryGetObjectManager(out IObjectManager objectManager)) return "Unable to resolve ObjectManager";
            if (!objectManager.TryGetObject(args[0], out Clan clan)) return $"Unable to find clan with id: {args[0]}";

            var stringBuilder = new StringBuilder();
            foreach (var field in typeof(Clan).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                stringBuilder.AppendLine($"{field.Name} = {field.GetValue(clan)}");
            }
            return stringBuilder.ToString();
        }

        // coop.debug.clan.add_influence <clanId> <amount>   (SERVER only)
        /// <summary>
        /// Authoritatively changes a clan's influence by the given amount via ChangeClanInfluenceAction so
        /// the _influence scalar-field store replicates; verify on both sides with coop.debug.clan.info.
        /// </summary>
        [CommandLineArgumentFunction("add_influence", "coop.debug.clan")]
        public static string AddClanInfluence(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count != 2) return "Usage: coop.debug.clan.add_influence <clanId> <amount>";
            if (!TryGetObjectManager(out IObjectManager objectManager)) return "Unable to resolve ObjectManager";
            if (!objectManager.TryGetObject(args[0], out Clan clan)) return $"Unable to find clan with id: {args[0]}";
            if (!float.TryParse(args[1], out float amount)) return $"'{args[1]}' is not a valid number";

            ChangeClanInfluenceAction.Apply(clan, amount);
            return $"Applied {amount} influence to '{clan.Name}'; clan is now at {clan.Influence} influence";
        }


        [CommandLineArgumentFunction("change_clan_leader", "coop.debug.clan")]
        public static string ChangeClanLeader(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.change_clan_leader <clanId> <heroId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string heroId = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!objectManager.TryGetObject(heroId, out Hero newLeader))
            {
                return $"Argument2: Kingdom not found by ID: {heroId}";
            }

            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, newLeader);

            return clan.Name.ToString() + " has a new leader: " + newLeader.Name.ToString();
        }

        [CommandLineArgumentFunction("change_clan_kingdom", "coop.debug.clan")]
        public static string ChangeClanKingdom(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.change_clan_kingdom <clanId> <kingdomId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string kingdomId = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!objectManager.TryGetObject(kingdomId, out Kingdom newKingdom))
            {
                return $"Argument2: Kingdom not found by ID: {kingdomId}";
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, newKingdom);

            return clan.Name.ToString() + " has join the kingdom : " + newKingdom.Name.ToString();
        }

        [CommandLineArgumentFunction("destroy_clan", "coop.debug.clan")]
        public static string DestroyClan(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count < 1)
            {
                return "Usage: coop.debug.clan.destroy_clan <clanId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            DestroyClanAction.Apply(clan);

            return clan.Name.ToString() + " has been destroyed";
        }

        [CommandLineArgumentFunction("add_companion", "coop.debug.clan")]
        public static string AddCompanion(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.add_companion <clanId> <heroId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string heroId = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!objectManager.TryGetObject(heroId, out Hero companion))
            {
                return $"Argument2: Hero not found by ID: {heroId}";
            }

            AddCompanionAction.Apply(clan, companion);

            return companion.Name.ToString() + " has joined " + clan.Name.ToString();
        }

        [CommandLineArgumentFunction("remove_companion", "coop.debug.clan")]
        public static string RemoveCompanion(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count < 1)
            {
                return "Usage: coop.debug.clan.remove_companion <heroId>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string heroId = args[0];

            if (!objectManager.TryGetObject(heroId, out Hero companion))
            {
                return $"Argument1: Hero not found by ID: {heroId}";
            }

            if (companion.Clan == null) return "Wanderer/companion is not in a clan.";

            var clanName = companion.Clan.Name;
            RemoveCompanionAction.ApplyByFire(companion.Clan, companion);

            return companion.Name.ToString() + " has left " + clanName.ToString();
        }

        [CommandLineArgumentFunction("add_renown", "coop.debug.clan")]
        public static string AddRenown(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count < 2)
            {
                return "Usage: coop.debug.clan.add_renown <clanId> <renown>";
            }

            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            string clanId = args[0];
            string renownStr = args[1];

            if (!objectManager.TryGetObject(clanId, out Clan clan))
            {
                return $"Argument1: Clan not found by ID: {clanId}";
            }

            if (!int.TryParse(renownStr, out int renown))
            {
                return $"Argument2: Renown {renownStr} is not a valid integer value.";
            }

            clan.AddRenown(renown);

            return clan.Name.ToString() + " given renown";
        }

        // coop.debug.clan.economy
        /// <summary>
        /// Read-only: prints a clan's battle-economy values (renown, influence, leader-party morale, and
        /// total troop xp). Run it on the host and on a client with the same clan id to compare the two.
        /// </summary>
        [CommandLineArgumentFunction("economy", "coop.debug.clan")]
        public static string ClanEconomy(List<string> args)
        {
            if (!TryGetObjectManager(out IObjectManager objectManager))
            {
                return "Unable to resolve ObjectManager";
            }

            Clan clan;
            if (args.Count >= 1)
            {
                // The argument can be a StringId, or a display name (which may contain spaces, so rejoin them).
                string query = string.Join(" ", args);

                if (!objectManager.TryGetObject(query, out clan))
                {
                    clan = Campaign.Current?.CampaignObjectManager?.Clans
                        ?.FirstOrDefault(c => string.Equals(c.Name?.ToString(), query, System.StringComparison.OrdinalIgnoreCase));
                }

                if (clan == null)
                {
                    return $"Clan not found by id or name: '{query}'";
                }
            }
            else
            {
                // No argument: use this instance's main hero clan (works on a client). The host has no main
                // hero, so pass the clan id or name printed by a client's output.
                clan = Hero.MainHero?.Clan;
                if (clan == null)
                {
                    return "No main hero on this instance; pass a clan id or name: coop.debug.clan.economy <clanIdOrName>";
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Clan '{clan.Name}' ({clan.StringId})");
            stringBuilder.AppendLine($"  Renown:    {clan.Renown}");
            stringBuilder.AppendLine($"  Influence: {clan.Influence}");

            var leaderParty = clan.Leader?.PartyBelongedTo;
            if (leaderParty != null)
            {
                int totalTroopXp = 0;
                var roster = leaderParty.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    totalTroopXp += roster.GetElementXp(i);
                }

                stringBuilder.AppendLine($"  Leader party '{leaderParty.Name}':");
                stringBuilder.AppendLine($"    RecentEventsMorale: {leaderParty.RecentEventsMorale}");
                stringBuilder.AppendLine($"    Total troop xp:     {totalTroopXp}");
            }

            return stringBuilder.ToString();
        }
        // coop.debug.clan.join_kingdom Player12 empire
        [CommandLineArgumentFunction("join_kingdom", "coop.debug.clan")]
        public static string JoinKingdom(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count != 2)
                return "Usage: coop.debug.clan.join_kingdom <clanId> <kingdomId>";

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return $"Unable to get {nameof(IObjectManager)}";

            if (objectManager.TryGetObject<Clan>(args[0], out var clan) == false)
                return $"Unable to get Clan with {args[0]}";

            if (objectManager.TryGetObject<Kingdom>(args[1], out var kingdom) == false)
                return $"Unable to get Kingdom with {args[1]}";

            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);

            return $"{clan.Name} joined {kingdom.Name}";
        }

        // coop.debug.clan.leave_kingdom Player12
        [CommandLineArgumentFunction("leave_kingdom", "coop.debug.clan")]
        public static string LeaveKingdom(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count != 1)
                return "Usage: coop.debug.clan.leave_kingdom <clanId>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            if (clan.Kingdom == null)
                return $"{clan.Name} does not belong to a kingdom";

            if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
                return $"Unable to get {nameof(IKingdomMembershipState)}";

            Kingdom previousKingdom = clan.Kingdom;
            string kingdomName = previousKingdom.Name.ToString();
            if (clan.IsUnderMercenaryService)
                ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(clan);
            else
                ChangeKingdomAction.ApplyByLeaveKingdom(clan);

            kingdomMembershipState.MoveClanToKingdom(
                previousKingdom,
                kingdom: null,
                clan: clan,
                publishCollectionChanges: true,
                republishExistingCollections: true);

            return $"{clan.Name} left {kingdomName}";
        }

        // coop.debug.clan.membership Player12
        [CommandLineArgumentFunction("membership", "coop.debug.clan")]
        public static string Membership(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: coop.debug.clan.membership <clanId>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            var sb = new StringBuilder();
            sb.AppendLine($"ClanId={clan.StringId}");
            sb.AppendLine($"KingdomId={clan.Kingdom?.StringId ?? "none"}");
            sb.AppendLine($"IsUnderMercenaryService={clan.IsUnderMercenaryService}");
            sb.AppendLine($"Tier={clan.Tier}");
            sb.AppendLine($"VassalEligibleTier={Campaign.Current.Models.ClanTierModel.VassalEligibleTier}");
            sb.AppendLine($"Influence={clan.Influence}");
            return sb.ToString();
        }

        // coop.debug.clan.give_influence Player12 500
        [CommandLineArgumentFunction("give_influence", "coop.debug.clan")]
        public static string GiveInfluence(List<string> args)
        {
            if (ModInformation.IsClient)
                return "Command is only available to run on the server";

            if (args.Count != 2)
                return "Usage: coop.debug.clan.give_influence <clanId> <amount>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            if (!float.TryParse(args[1], out float amount))
                return $"Unable to parse {args[1]} as float";

            ChangeClanInfluenceAction.Apply(clan, amount);

            return $"Gave {amount} influence to {clan.Name}";
        }
        // coop.debug.clan.info
        [CommandLineArgumentFunction("info", "coop.debug.clan")]
        public static string InfoClan(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: coop.debug.clan.info <clanId>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {clan.Name}");
            sb.AppendLine($"StringId: {clan.StringId}");
            sb.AppendLine($"Leader: {clan.Leader?.Name.ToString() ?? "none"}");
            sb.AppendLine($"Kingdom: {clan.Kingdom?.Name.ToString() ?? "none"}");
            sb.AppendLine($"Influence: {clan.Influence}");
            sb.AppendLine($"Renown: {clan.Renown}");
            sb.AppendLine($"Tier: {clan.Tier}");
            sb.AppendLine($"IsEliminated: {clan.IsEliminated}");
            sb.AppendLine($"Members: {string.Join(", ", clan.Heroes.Select(h => h.Name))}");
            return sb.ToString();
        }

        [CommandLineArgumentFunction("daily_gold_change", "coop.debug.clan")]
        public static string ViewPredicatedDailyGoldChange(List<string> args)
        {
            if (args.Count != 1)
                return "Usage: coop.debug.clan.daily_gold_change <clanId>";

            if (!TryGetObjectManager(out IObjectManager objectManager))
                return "Unable to resolve ObjectManager";

            if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                return $"Unable to get Clan with {args[0]}";

            var goldChange = Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(clan, true, false, true);

            var sb = new StringBuilder();
            foreach (var explanation in goldChange._explainer.Lines)
            {
                sb.AppendLine($"{explanation.Name}: {explanation.Number}");
            }
            sb.AppendLine($"Total: {goldChange.ResultNumber}");

            return sb.ToString();
        }
    }
}
//coop.debug.clan.add_renown Player 1000
// coop.debug.clan.join_kingdom Player empire
//coop.debug.clan.give_influence Player 500
