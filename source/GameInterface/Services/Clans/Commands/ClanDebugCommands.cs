using Common.Commands;
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

namespace GameInterface.Services.GameDebug.Commands
{
    public class ClanDebugCommands
    {
        private static CoopCommandResult Succeeded(string output) =>
            new CoopCommandResult(true, output);

        private static CoopCommandResult Failed(string output) =>
            new CoopCommandResult(false, output, "command_failed");

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

            public sealed class ClanOpenCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "open";

        public string Description => "Opens the clan screen on a client.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
                if (Game.Current?.GameStateManager == null) return Failed("The game-state manager is unavailable.");
                if (Game.Current.GameStateManager.ActiveState is ClanState) return Succeeded("CLAN_SCREEN_ALREADY_OPEN");
                if (Hero.MainHero == null || Hero.MainHero.IsDead)
                    return Failed("The local main hero is unavailable.");

                Game.Current.GameStateManager.PushState(
                    Game.Current.GameStateManager.CreateState<ClanState>(), 0);
                return Succeeded("CLAN_SCREEN_OPENED");
        }
    }

            public sealed class ClanCloseCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "close";

        public string Description => "Closes the clan screen on a client.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
                if (!(Game.Current?.GameStateManager?.ActiveState is ClanState))
                    return Failed("No active Clan screen.");

                Game.Current.GameStateManager.PopState(0);
                return Succeeded("CLAN_SCREEN_CLOSED");
        }
    }

            public sealed class ClanScreenStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "screen_state";

        public string Description => "Reports clan screen state.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");

                var clanScreen = ScreenManager.TopScreen as GauntletClanScreen;
                return Succeeded($"CLAN_SCREEN_STATE active={Game.Current?.GameStateManager?.ActiveState is ClanState} " +
                    $"topScreen={clanScreen != null} dataSource={clanScreen?._dataSource != null} " +
                    $"parties={clanScreen?._dataSource?.ClanParties?._parties?.Count ?? -1} " +
                    $"partiesSelected={clanScreen?._dataSource?.IsPartiesSelected ?? false} " +
                    $"mainHero={Hero.MainHero?.StringId ?? "none"}");
        }
    }

            public sealed class ClanSelectPartiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "select_parties";

        public string Description => "Selects the parties tab on the clan screen.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");

                var clanScreen = ScreenManager.TopScreen as GauntletClanScreen;
                if (clanScreen?._dataSource == null) return Failed("The Clan screen is unavailable.");

                clanScreen._dataSource.SetSelectedCategory(1);
                return Succeeded($"CLAN_PARTIES_SELECTED parties={clanScreen._dataSource.ClanParties?._parties?.Count ?? -1}");
        }
    }

            public sealed class ClanWageStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "wage_state";

        public string Description => "Reports clan party wage state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The optional registered clan id.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
                if (Campaign.Current?.Models?.PartyWageModel == null) return Failed("The party wage model is unavailable.");
                if (!TryGetObjectManager(out var objectManager)) return Failed("Unable to resolve ObjectManager.");

                Clan clan = Clan.PlayerClan;
                if (args.Count == 1 && !objectManager.TryGetObject(args[0], out clan))
                    return Failed($"Unable to find clan with id: {args[0]}");
                if (clan == null) return Failed("The target clan is unavailable.");

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

                return output.Length == 0
                    ? Failed("No Clan-screen parties were found.")
                    : Succeeded(output.ToString());
        }
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

            public sealed class ClanRefreshBurstCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "refresh_burst";

        public string Description => "Sends repeated party role refresh messages.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_id", "The registered mobile party id."),
            new ExpectedArgs("count", "A message count from 1 through 500."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!ModInformation.IsServer) return Failed("Command can only be run on the server.");
                if (!int.TryParse(args[1], out var count) || count < 1 || count > 500)
                    return Failed($"Refresh count must be an integer from 1 through 500: {args[1]}.");
                if (!TryGetObjectManager(out var objectManager)) return Failed("Unable to resolve ObjectManager.");
                if (!objectManager.TryGetObject(args[0], out MobileParty _))
                    return Failed($"Party with id {args[0]} not found.");
                if (!ContainerProvider.TryResolve<INetwork>(out var network))
                    return Failed("Unable to resolve Network.");

                for (int i = 0; i < count; i++)
                {
                    network.SendAll(new RefreshAfterRoleAssignment(args[0]));
                }

                return Succeeded($"REFRESH_BURST_SENT party={args[0]} count={count}");
        }
    }

        // coop.debug.clan.list
        /// <summary>
        /// Lists all the clans
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the clans</returns>

            public sealed class ClanListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "list";

        public string Description => "Lists campaign clans.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                StringBuilder stringBuilder = new StringBuilder();

                List<Clan> clans = Campaign.Current.CampaignObjectManager.Clans.ToList();

                clans.ForEach((clan) =>
                {
                    stringBuilder.AppendLine(string.Format("ID: '{0}' Name: '{1}'", clan.StringId, clan.Name));
                });

                return Succeeded(stringBuilder.ToString());
        }
    }

        // coop.debug.clan.field_dump <clanId>
        /// <summary>
        /// Reflection-dumps every field of a Clan so a server screenshot and a client screenshot can be
        /// compared field-for-field to confirm Clan field syncs still replicate.
        /// </summary>

            public sealed class ClanFieldDumpCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "field_dump";

        public string Description => "Dumps every field of a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!TryGetObjectManager(out IObjectManager objectManager)) return Failed("Unable to resolve ObjectManager");
                if (!objectManager.TryGetObject(args[0], out Clan clan)) return Failed($"Unable to find clan with id: {args[0]}");

                var stringBuilder = new StringBuilder();
                foreach (var field in typeof(Clan).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                {
                    stringBuilder.AppendLine($"{field.Name} = {field.GetValue(clan)}");
                }
                return Succeeded(stringBuilder.ToString());
        }
    }

        // coop.debug.clan.add_influence <clanId> <amount>   (SERVER only)
        /// <summary>
        /// Authoritatively changes a clan's influence by the given amount via ChangeClanInfluenceAction so
        /// the _influence scalar-field store replicates; verify on both sides with coop.debug.clan.info.
        /// </summary>

            public sealed class ClanAddInfluenceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "add_influence";

        public string Description => "Adds influence to a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("amount", "The numeric influence amount."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager)) return Failed("Unable to resolve ObjectManager");
                if (!objectManager.TryGetObject(args[0], out Clan clan)) return Failed($"Unable to find clan with id: {args[0]}");
                if (!float.TryParse(args[1], out float amount)) return Failed($"'{args[1]}' is not a valid number");

                ChangeClanInfluenceAction.Apply(clan, amount);
                return Succeeded($"Applied {amount} influence to '{clan.Name}'; clan is now at {clan.Influence} influence");
        }
    }

            public sealed class ClanChangeLeaderCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "change_clan_leader";

        public string Description => "Changes the leader of a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("hero_id", "The registered new leader hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                string clanId = args[0];
                string heroId = args[1];

                if (!objectManager.TryGetObject(clanId, out Clan clan))
                {
                    return Failed($"Argument1: Clan not found by ID: {clanId}");
                }

                if (!objectManager.TryGetObject(heroId, out Hero newLeader))
                {
                    return Failed($"Argument2: Kingdom not found by ID: {heroId}");
                }

                ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, newLeader);

                return Succeeded(clan.Name.ToString() + " has a new leader: " + newLeader.Name.ToString());
        }
    }

            public sealed class ClanChangeKingdomCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "change_clan_kingdom";

        public string Description => "Moves a registered clan to a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                string clanId = args[0];
                string kingdomId = args[1];

                if (!objectManager.TryGetObject(clanId, out Clan clan))
                {
                    return Failed($"Argument1: Clan not found by ID: {clanId}");
                }

                if (!objectManager.TryGetObject(kingdomId, out Kingdom newKingdom))
                {
                    return Failed($"Argument2: Kingdom not found by ID: {kingdomId}");
                }

                ChangeKingdomAction.ApplyByJoinToKingdom(clan, newKingdom);

                return Succeeded(clan.Name.ToString() + " has join the kingdom : " + newKingdom.Name.ToString());
        }
    }

            public sealed class ClanDestroyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "destroy_clan";

        public string Description => "Destroys a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                string clanId = args[0];

                if (!objectManager.TryGetObject(clanId, out Clan clan))
                {
                    return Failed($"Argument1: Clan not found by ID: {clanId}");
                }

                DestroyClanAction.Apply(clan);

                return Succeeded(clan.Name.ToString() + " has been destroyed");
        }
    }

            public sealed class ClanAddCompanionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "add_companion";

        public string Description => "Adds a registered companion to a clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("hero_id", "The registered companion hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                string clanId = args[0];
                string heroId = args[1];

                if (!objectManager.TryGetObject(clanId, out Clan clan))
                {
                    return Failed($"Argument1: Clan not found by ID: {clanId}");
                }

                if (!objectManager.TryGetObject(heroId, out Hero companion))
                {
                    return Failed($"Argument2: Hero not found by ID: {heroId}");
                }

                AddCompanionAction.Apply(clan, companion);

                return Succeeded(companion.Name.ToString() + " has joined " + clan.Name.ToString());
        }
    }

            public sealed class ClanRemoveCompanionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "remove_companion";

        public string Description => "Removes a registered companion from a clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered companion hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                string heroId = args[0];

                if (!objectManager.TryGetObject(heroId, out Hero companion))
                {
                    return Failed($"Argument1: Hero not found by ID: {heroId}");
                }

                if (companion.Clan == null) return Failed("Wanderer/companion is not in a clan.");

                var clanName = companion.Clan.Name;
                RemoveCompanionAction.ApplyByFire(companion.Clan, companion);

                return Succeeded(companion.Name.ToString() + " has left " + clanName.ToString());
        }
    }

            public sealed class ClanAddRenownCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "add_renown";

        public string Description => "Adds renown to a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("renown", "The integer renown amount."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                string clanId = args[0];
                string renownStr = args[1];

                if (!objectManager.TryGetObject(clanId, out Clan clan))
                {
                    return Failed($"Argument1: Clan not found by ID: {clanId}");
                }

                if (!int.TryParse(renownStr, out int renown))
                {
                    return Failed($"Argument2: Renown {renownStr} is not a valid integer value.");
                }

                clan.AddRenown(renown);

                return Succeeded(clan.Name.ToString() + " given renown");
        }
    }

        // coop.debug.clan.economy
        /// <summary>
        /// Read-only: prints a clan's battle-economy values (renown, influence, leader-party morale, and
        /// total troop xp). Run it on the host and on a client with the same clan id to compare the two.
        /// </summary>

            public sealed class ClanEconomyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "economy";

        public string Description => "Reports battle-economy values for a clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id_or_name", "The optional clan id or display name. Quote multi-word names.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!TryGetObjectManager(out IObjectManager objectManager))
                {
                    return Failed("Unable to resolve ObjectManager");
                }

                Clan clan;
                if (args.Count >= 1)
                {
                    // Quote a multi-word display name so it arrives as one command argument.
                    string query = args[0];

                    if (!objectManager.TryGetObject(query, out clan))
                    {
                        clan = Campaign.Current?.CampaignObjectManager?.Clans
                            ?.FirstOrDefault(c => string.Equals(c.Name?.ToString(), query, System.StringComparison.OrdinalIgnoreCase));
                    }

                    if (clan == null)
                    {
                        return Failed($"Clan not found by id or name: '{query}'");
                    }
                }
                else
                {
                    // No argument: use this instance's main hero clan (works on a client). The host has no main
                    // hero, so pass the clan id or name printed by a client's output.
                    clan = Hero.MainHero?.Clan;
                    if (clan == null)
                    {
                        return Failed("No main hero on this instance; pass a clan id or name: coop.debug.clan.economy <clanIdOrName>");
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

                return Succeeded(stringBuilder.ToString());
        }
    }
        // coop.debug.clan.join_kingdom Player12 empire

            public sealed class ClanJoinKingdomCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "join_kingdom";

        public string Description => "Joins a registered clan to a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                    return Failed($"Unable to get {nameof(IObjectManager)}");

                if (objectManager.TryGetObject<Clan>(args[0], out var clan) == false)
                    return Failed($"Unable to get Clan with {args[0]}");

                if (objectManager.TryGetObject<Kingdom>(args[1], out var kingdom) == false)
                    return Failed($"Unable to get Kingdom with {args[1]}");

                ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);

                return Succeeded($"{clan.Name} joined {kingdom.Name}");
        }
    }

        // coop.debug.clan.leave_kingdom Player12

            public sealed class ClanLeaveKingdomCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "leave_kingdom";

        public string Description => "Removes a registered clan from its kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                    return Failed("Unable to resolve ObjectManager");

                if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                    return Failed($"Unable to get Clan with {args[0]}");

                if (clan.Kingdom == null)
                    return Failed($"{clan.Name} does not belong to a kingdom");

                if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
                    return Failed($"Unable to get {nameof(IKingdomMembershipState)}");

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

                return Succeeded($"{clan.Name} left {kingdomName}");
        }
    }

        // coop.debug.clan.membership Player12

            public sealed class ClanMembershipCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "membership";

        public string Description => "Reports kingdom membership for a clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!TryGetObjectManager(out IObjectManager objectManager))
                    return Failed("Unable to resolve ObjectManager");

                if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                    return Failed($"Unable to get Clan with {args[0]}");

                var sb = new StringBuilder();
                sb.AppendLine($"ClanId={clan.StringId}");
                sb.AppendLine($"KingdomId={clan.Kingdom?.StringId ?? "none"}");
                sb.AppendLine($"IsUnderMercenaryService={clan.IsUnderMercenaryService}");
                sb.AppendLine($"Tier={clan.Tier}");
                sb.AppendLine($"VassalEligibleTier={Campaign.Current.Models.ClanTierModel.VassalEligibleTier}");
                sb.AppendLine($"Influence={clan.Influence}");
                return Succeeded(sb.ToString());
        }
    }

        // coop.debug.clan.give_influence Player12 500

            public sealed class ClanGiveInfluenceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "give_influence";

        public string Description => "Gives influence to a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
            new ExpectedArgs("amount", "The numeric influence amount."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (ModInformation.IsClient)
                    return Failed("Command is only available to run on the server");

                if (!TryGetObjectManager(out IObjectManager objectManager))
                    return Failed("Unable to resolve ObjectManager");

                if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                    return Failed($"Unable to get Clan with {args[0]}");

                if (!float.TryParse(args[1], out float amount))
                    return Failed($"Unable to parse {args[1]} as float");

                ChangeClanInfluenceAction.Apply(clan, amount);

                return Succeeded($"Gave {amount} influence to {clan.Name}");
        }
    }
        // coop.debug.clan.info

            public sealed class ClanInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "info";

        public string Description => "Reports a curated summary for a registered clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!TryGetObjectManager(out IObjectManager objectManager))
                    return Failed("Unable to resolve ObjectManager");

                if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                    return Failed($"Unable to get Clan with {args[0]}");

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
                return Succeeded(sb.ToString());
        }
    }

            public sealed class ClanDailyGoldChangeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.clan";

        public string Name => "daily_gold_change";

        public string Description => "Reports predicted daily gold changes for a clan.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan_id", "The registered clan id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
                if (!TryGetObjectManager(out IObjectManager objectManager))
                    return Failed("Unable to resolve ObjectManager");

                if (!objectManager.TryGetObject<Clan>(args[0], out var clan))
                    return Failed($"Unable to get Clan with {args[0]}");

                var goldChange = Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(clan, true, false, true);

                var sb = new StringBuilder();
                foreach (var explanation in goldChange._explainer.Lines)
                {
                    sb.AppendLine($"{explanation.Name}: {explanation.Number}");
                }
                sb.AppendLine($"Total: {goldChange.ResultNumber}");

                return Succeeded(sb.ToString());
        }
    }
    }
}
//coop.debug.clan.add_renown Player 1000
// coop.debug.clan.join_kingdom Player empire
//coop.debug.clan.give_influence Player 500
