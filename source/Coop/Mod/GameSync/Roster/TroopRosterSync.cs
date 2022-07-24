using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using Sync.Behaviour;
using Sync.Call;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace Coop.Mod.GameSync.Roster
{
    /// <summary>
    ///     Implements synchronization of TroopRoster changes. The synchronization works as follows:
    ///     
    ///     1. Whenever a TroopRoster on the serverside is changed, a RPC is issued to transfer the updated TroopRoster. 
    ///        The RPC is scoped to the party that owns the TroopRoster. Meaning if the party of the roster is outside the 
    ///        scope of a client, that client will not receive an update.
    ///     2. [TODO] Since the RPC is scoped, clients will not have all TroopRoster on the map up to date. So additionally, the
    ///        rosters are updated wheneveer a party enters the scope of a client.
    /// </summary>
    class TroopRosterSync
    {
        [PatchInitializer]
        private static void InitRPC()
        {
            OnTroopRosterUpdateRPC = new Invokable(typeof(TroopRosterSync).GetMethod(nameof(TroopRosterSync.OnTroopRosterUpdate), BindingFlags.NonPublic | BindingFlags.Static));
        }

        /// <summary>
        ///     RPC that is called when a troop roster is changed.
        /// </summary>
        private static Invokable OnTroopRosterUpdateRPC;
        private static void OnTroopRosterUpdate(MobileParty owner, TroopRoster roster)
        {
            if (Coop.IsServer)
            {
                return;
            }

            Logger.Trace($"Received TroopRoster update for {roster}");
            if (owner == null)
            {
                Logger.Warn($"TroopRoster update failed, no owner set.");
            }

            if(owner == MobileParty.MainParty)
            {
                Logger.Error($"Known bug. The roster of the main party includes the main hero whose GUID is somehow wrong. Skipped roster update.");
                return;
            }

            // It's tricky to swap out the troop roster of an already existing party. Instead, we update the existing roster to match the one we received.
            if (roster.IsPrisonRoster)
            {
                owner.PrisonRoster.Clear();
                owner.PrisonRoster.Add(roster);
                Logger.Trace($"Updated prison roster for {owner}.");
            }
            else
            {
                owner.MemberRoster.Clear();
                owner.MemberRoster.Add(roster);
                Logger.Trace($"Updated member roster for {owner}.");
            }
        }

        /// <summary>
        ///     Broadcast a change to a troop roster to all clients that have the roster in scope.
        /// </summary>
        /// <param name="roster">The roster to broadcast.</param>
        public static void BroadcastTroopRosterChange(TroopRoster roster)
        {
            MobileParty owner = (typeof(TroopRoster)
                        .GetField("<OwnerParty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(roster) as PartyBase)?.MobileParty;
            BroadcastTroopRosterChange(owner, roster);
        }

        /// <summary>
        ///     Broadcast a change to a troop roster to all clients that have the roster in scope.
        /// </summary>
        /// <param name="owner">The owner of the roster.</param>
        /// <param name="roster">The roster to broadcast.</param>
        public static void BroadcastTroopRosterChange([NotNull] MobileParty owner, TroopRoster roster)
        {
            if (owner != null &&
                CoopServer.Instance.Persistence.MobilePartyEntityManager.TryGetEntity(owner, out MobilePartyEntityServer entity))
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    CharacterObject character = roster.GetCharacterAtIndex(i);
                    if(character != null)
                    {
                        CharacterObjectSync.AssertCharacterIsRegistered(character);
                    }
                }

                // The party this roster belongs to is managed. Send and update.
                var entities = new EntityId[] { entity.Id };
                CoopServer.Instance.Synchronization.Broadcast(entities, OnTroopRosterUpdateRPC.Id, null, new object[] { owner, roster });
            }
        }

        /// <summary>
        ///     We want to be notified whenever a TroopRoster changes. There is a field `VersionNo` that is supposed
        ///     to keep track of that as well, but as of game version 1.7.0 the implementation is not usable for us.
        ///     Reason being, that is being updated inconsistently and redundantly.
        /// </summary>
        [HarmonyPatch]
        class PatchTroopRosterVersion
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                return typeof(TroopRoster).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method =>
                    {
                        // State management in this class is a mess...
                        return method.Name.Equals("OnNumberChanged") ||
                        method.Name.Equals("AddNewElement") ||
                        method.Name.Equals("SetElementNumber") ||
                        method.Name.Equals("SetElementWoundedNumber") ||
                        method.Name.Equals("SetElementXp");
                        // method.Name.Equals("SlideTroops") || 
                        // method.Name.Equals("RemoveZeroCounts") ||
                        // method.Name.Equals("AddTroopTempXp") ||
                        // method.Name.Equals("ClearTempXp") ||
                        // method.Name.Equals("ClampConformity") ||
                        // method.Name.Equals("ClampXp");
                    })
                    .Cast<MethodBase>();
            }
            static void Postfix(TroopRoster __instance)
            {
                if (!Coop.IsServer)
                {
                    return;
                }
                BroadcastTroopRosterChange(__instance);
            }
        }

        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    }
}
