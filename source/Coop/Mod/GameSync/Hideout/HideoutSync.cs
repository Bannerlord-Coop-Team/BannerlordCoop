using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Common;
using Coop.Mod.GameSync.Roster;
using Coop.Mod.Persistence.Party;
using CoopFramework;
using HarmonyLib;
using RailgunNet.System.Types;
using Sync.Call;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Coop.Mod.GameSync.Hideout
{
    class HideoutSync
    {
        private static Invokable OnHideoutDiscoveryRPC;
        private static Invokable OnHideoutRemovalRPC;

        /// <summary>
        ///     Initialize RPCs on client and server side.
        /// </summary>
        [PatchInitializer]
        private static void InitRPC()
        {
            OnHideoutDiscoveryRPC = new Invokable(typeof(HideoutSync).GetMethod(nameof(HideoutSync.OnHideoutDiscovery), BindingFlags.NonPublic | BindingFlags.Static));
            OnHideoutRemovalRPC = new Invokable(typeof(HideoutSync).GetMethod(nameof(HideoutSync.OnHideoutRemoval), BindingFlags.NonPublic | BindingFlags.Static));
        }

        /// <summary>
        ///     RPC that is called when an client discover an hideout.
        /// </summary>
        /// <param name="settlementGuid">Settlement GUID</param>
        private static void OnHideoutDiscovery(Guid settlementGuid)
        {
            if (Coop.IsServer)
            {
                return;
            }

            Settlement settlement = CoopObjectManager.GetObject<Settlement>(settlementGuid);

            if (settlement == null)
            {
                return;
            }

            settlement.IsVisible = true;
            settlement.Hideout.IsSpotted = true;
            CampaignEventDispatcher.Instance.OnHideoutSpotted(PartyBase.MainParty, settlement.Party);
        }

        /// <summary>
        ///       RPC that is called when an hideout is empty and being removed from the map visually.
        /// </summary>
        /// <param name="settlementGuid">Settlement GUID</param>
        private static void OnHideoutRemoval(Guid settlementGuid)
        {
            if (Coop.IsServer)
            {
                return;
            }

            Settlement settlement = CoopObjectManager.GetObject<Settlement>(settlementGuid);

            if (settlement == null)
            {
                return;
            }

            typeof(TaleWorlds.CampaignSystem.Hideout).GetMethod("OnHideoutIsEmpty", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(settlement.Hideout, new object[] { });
        }

        /// <summary>
        ///     Broadcast to specific client when they discovered a new hideout.
        /// </summary>
        /// <param name="mobileParty">MobileParty of the client</param>
        /// <param name="settlement">Discovered hideout</param>
        public static void BroadcastHideoutDiscovery(MobileParty mobileParty, Settlement settlement)
        {
            if (mobileParty != null &&
                CoopServer.Instance.Persistence.MobilePartyEntityManager.TryGetEntity(mobileParty, out MobilePartyEntityServer entity))
            {
                Guid settlementGuid = CoopObjectManager.GetGuid(settlement);

                if (settlementGuid == Guid.Empty)
                {
                    return;
                }

                var entities = new EntityId[] {entity.Id};
                CoopServer.Instance.Synchronization.Broadcast(entities, OnHideoutDiscoveryRPC.Id, null, new object[] { settlementGuid });
            }
        }

        /// <summary>
        ///     Broadcast to clients when an Hideout has been removed/deleted.
        /// </summary>
        /// <param name="settlement">Settlement to remove</param>
        public static void BroadcastHideoutRemoval(Settlement settlement)
        {
            Guid settlementGuid = CoopObjectManager.GetGuid(settlement);

            if (settlementGuid == Guid.Empty)
            {
                return;
            }

            CoopServer.Instance.Synchronization.Broadcast(OnHideoutRemovalRPC.Id, null, new object[] { settlementGuid });
        }
    }
}
