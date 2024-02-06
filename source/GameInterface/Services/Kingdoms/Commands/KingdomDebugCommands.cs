using Autofac;
using Common.Extensions;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands
{
    public static class KingdomDebugCommands
    {
        private static readonly Func<Kingdom, MBList<KingdomDecision>> GetUnresolvedDecisions = typeof(Kingdom).GetField("_unresolvedDecisions", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Kingdom, MBList<KingdomDecision>>();
        private static readonly Dictionary<string, Func<IObjectManager, List<string>, Clan, KingdomDecision>> GetKingdomDecisionFunc = new Dictionary<string, Func<IObjectManager, List<string>, Clan, KingdomDecision>>()
        {
            { nameof(DeclareWarDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) => 
                {
                    string factionId = args[4];
                    if(!objectManager.TryGetObject(factionId, out MBObjectBase faction) || !(faction is IFaction))
                    {
                        return null;
                    }
                    return new DeclareWarDecision(proposerClan, (IFaction)faction);
                } 
            },
            { nameof(ExpelClanFromKingdomDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) =>
                {
                    string clanId = args[4];
                    if(!objectManager.TryGetObject(clanId, out Clan clan))
                    {
                        return null;
                    }
                    return new ExpelClanFromKingdomDecision(proposerClan, clan);
                }
            },
            { nameof(KingSelectionKingdomDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) =>
                {
                    string clanId = args[4];
                    if(!objectManager.TryGetObject(clanId, out Clan clan))
                    {
                        return null;
                    }
                    return new KingSelectionKingdomDecision(proposerClan, clan);
                }
            },
            { nameof(KingdomPolicyDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) =>
                {
                    string policyId = args[4];
                    string isInvertedDecision = args[5];

                    if(!objectManager.TryGetObject(policyId, out PolicyObject policy) || !bool.TryParse(isInvertedDecision, out bool isInverted))
                    {
                        return null;
                    }

                    return new KingdomPolicyDecision(proposerClan, policy, isInverted);
                }
            },
            { nameof(SettlementClaimantDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) =>
                {
                    string settlementId = args[4];
                    string capturerHeroId = args[5];
                    string clanToExcludeId = args[6];

                    if(!objectManager.TryGetObject(settlementId, out Settlement settlement) ||
                    !objectManager.TryGetObject(capturerHeroId, out Hero capturerHero) ||
                    !objectManager.TryGetObject(clanToExcludeId, out Clan clanToExclude))
                    {
                        return null;
                    }

                    return new SettlementClaimantDecision(proposerClan, settlement, capturerHero, clanToExclude);
                }
            },
            { nameof(SettlementClaimantPreliminaryDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) =>
                {
                    string settlementId = args[3];

                    if(!objectManager.TryGetObject(settlementId, out Settlement settlement))
                    {
                        return null;
                    }

                    return new SettlementClaimantPreliminaryDecision(proposerClan, settlement);
                }
            },
            { nameof(MakePeaceKingdomDecision), (IObjectManager objectManager, List<string> args, Clan proposerClan) =>
                {
                    string factionId = args[4];
                    string dailyTribute = args[5];
                    string applyResults = args[6];

                    if(!objectManager.TryGetObject(factionId, out MBObjectBase faction) || !(faction is IFaction) || !int.TryParse(dailyTribute, out int dailyTributeToBePaid) || !bool.TryParse(applyResults, out bool applyResult))
                    {
                        return null;
                    }

                    return new MakePeaceKingdomDecision(proposerClan, (IFaction)faction, dailyTributeToBePaid, applyResult);
                }
            },
        };


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

        // coop.debug.kingdom.list
        /// <summary>
        /// Lists all the Kingdoms
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the kingdoms</returns>
        [CommandLineArgumentFunction("list_kingdoms", "coop.debug.kingdom")]
        public static string ListKingdoms(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Kingdom> kingdoms = Campaign.Current.CampaignObjectManager.Kingdoms;

            kingdoms.ForEach((kingdom) =>
            {
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", kingdom.StringId, kingdom.Name));
            });

            return stringBuilder.ToString();
        }

        // coop.debug.kingdom.list_decisions
        /// <summary>
        /// Lists all the decisions of a specific kingdom.
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the decisions of a specific kingdom</returns>
        [CommandLineArgumentFunction("list_kingdom_decisions", "coop.debug.kingdom")]
        public static string ListKingdomDecisions(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.kingdom.list_kingdom_decisions <kingdomId>";
            }

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return "Unable to resolve ObjectManager";
            }

            if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
            {
                return $"ID: '{args[0]}' not found";
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"Kingdom decisions of Kingdom: {kingdom.Name}");

            int i = 1;
            foreach (KingdomDecision kingdomDecision in kingdom.UnresolvedDecisions)
            {
                stringBuilder.Append($"{i}. {kingdomDecision.GetType().Name}");
                i++;
            }

            return stringBuilder.ToString();
        }

        // coop.debug..kingdom.add_decision
        /// <summary>
        /// Adds a decision to a Kingdom.
        /// </summary>
        /// <param name="args">first arg : kingdomId ; second arg : decision to add</param>
        /// <returns></returns>
        [CommandLineArgumentFunction("add_decision", "coop.debug.kingdom")]
        public static string AddDecision(List<string> args)
        {
            if (args.Count < 3)
            {
                return "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> <decisionType> <OtherArgs>";
            }

            string kingdomId = args[0];
            string clanId = args[1];
            string ignoreInfluence = args[2];
            string decisionType = args[3];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return "Unable to resolve ObjectManager";
            }
            if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
            {
                return $"Kingdom with ID: '{kingdomId}' not found";
            }

            if (objectManager.TryGetObject(clanId, out Clan proposerClan) == false)
            {
                return $"Clan with ID: '{clanId}' not found";
            }

            if (!bool.TryParse(ignoreInfluence, out bool ignoreInfluenceCost))
            {
                return $"Couldnt convert ignoreInfluenceCost: {ignoreInfluence}";
            }

            if (!GetKingdomDecisionFunc.ContainsKey(decisionType))
            {
                return $"Kingdom decision type: {decisionType} does not exist.";
            }

            kingdom.AddDecision(GetKingdomDecisionFunc[decisionType](objectManager, args, proposerClan), ignoreInfluenceCost);
            return $"Kingdom decision added:";
        }

        // coop.debug.kingdom.remove_decision
        /// <summary>
        /// Removes a decision from a Kingdom
        /// </summary>
        /// <param name="args">first arg : kingdomId ; second arg : index of decision to remove</param>
        /// <returns></returns>
        [CommandLineArgumentFunction("remove_decision", "coop.debug.kingdom")]
        public static string RemoveDecision(List<string> args)
        {
            if (args.Count != 2)
            {
                return "Usage: coop.debug.kingdom.remove_decision <kingdomId> <Index>";
            }

            string kingdomId = args[0];
            string index = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return "Unable to resolve ObjectManager";
            }
            if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
            {
                return $"Kingdom with ID: '{kingdomId}' not found";
            }

            if (!int.TryParse(index, out int idx))
            {
                return $"Argument2: {index} is not a number.";
            }

            var decisions = GetUnresolvedDecisions(kingdom);
            if (idx > 0 && idx <= decisions.Count)
            {
                kingdom.RemoveDecision(decisions[idx - 1]);
            }
            else 
            {
                return "Index is out of bounds.";
            }

            return $"Kingdom decision removed:";
        }

    }
}
