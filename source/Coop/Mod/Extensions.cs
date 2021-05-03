﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Coop.Mod.Patch;
using Coop.Mod.Persistence.Party;
using Sync.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod
{
    public static class Extensions
    {
        /// <summary>
        ///     Conversion from a RailGun <see cref="MovementState"/>.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static MovementData ToData(this MovementState state)
        {
            return new MovementData
            {
                DefaultBehaviour = state.DefaultBehavior,
                TargetPosition = state.TargetPosition,
                TargetParty = state.TargetPartyIndex != Coop.InvalidId
                    ? MBObjectManager.Instance.GetObject(state.TargetPartyIndex) as
                        MobileParty
                    : null,
                TargetSettlement = state.SettlementIndex != Coop.InvalidId
                    ? MBObjectManager.Instance.GetObject(
                        state.SettlementIndex) as Settlement
                    : null
            };
        }
        /// <summary>
        ///     Conversion to a RailGun <see cref="MovementState"/>.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static MovementState ToState(this MovementData data)
        {
            return new MovementState
            {
                DefaultBehavior = data.DefaultBehaviour,
                TargetPosition = data.TargetPosition,
                TargetPartyIndex = data.TargetParty?.Id ?? Coop.InvalidId,
                SettlementIndex = data.TargetSettlement?.Id ?? Coop.InvalidId
            };
        }
        /// <summary>
        ///     Returns the set of movement relevant data.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static MovementData GetMovementData(this MobileParty party)
        {
            return new MovementData()
            {
                DefaultBehaviour = party.DefaultBehavior,
                TargetParty = party.TargetParty,
                TargetSettlement =  party.TargetSettlement,
                TargetPosition = party.TargetPosition,
                NumberOfFleeingsAtLastTravel = party.NumberOfFleeingsAtLastTravel
            };
        }
        /// <summary>
        ///     Returns whether the given <see cref="MobileParty"/> is the main party of any player, remote or local.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsAnyPlayerMainParty(this MobileParty party)
        {
            return Coop.IsAnyPlayerMainParty(party);
        }
        /// <summary>
        ///     Returns whether the given <see cref="MobileParty"/> is the main party of the local game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsLocalPlayerMainParty(this MobileParty party)
        {
            return Coop.IsLocalPlayerMainParty(party);
        }
        /// <summary>
        ///     Returns whether the given <see cref="MobileParty"/> is the main party of a remote game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsRemotePlayerMainParty(this MobileParty party)
        {
            return Coop.IsRemotePlayerMainParty(party);
        }
        public static string ToFriendlyString(this LoadGameResult loadResult)
        {
            if (!loadResult.LoadResult.Successful)
            {
                return "Error during load.";
            }

            string sRet = "Loading successful.";
            if (loadResult.ModuleCheckResults.Count > 0)
            {
                sRet += "Module missmatches in loaded file:";
                for (int i = 0; i < loadResult.ModuleCheckResults.Count; i++)
                {
                    ModuleCheckResult module = loadResult.ModuleCheckResults[i];
                    sRet += Environment.NewLine + $"[{i}] {module.ModuleName}: {module.Type}.";
                }
            }

            return sRet;
        }

        public static string ToFriendlyString(this LoadResult loadResult)
        {
            string sRet = "Loading " + (loadResult.Successful ? "success. " : "failed. ");
            if (loadResult.MetaData == null)
            {
                sRet += "No meta data";
            }
            else
            {
                sRet += loadResult.MetaData;
            }

            sRet += Environment.NewLine;

            sRet += loadResult.Errors.Length + " errors:";
            foreach (LoadError error in loadResult.Errors)
            {
                sRet += Environment.NewLine + error.ToFriendlyString();
            }

            return sRet;
        }

        public static string ToFriendlyString(this LoadError error)
        {
            return $"Error: {error.Message}";
        }

        public static byte[] GetBuffer(this InMemDriver driver)
        {
            return Utils.GetPrivateField<byte[]>(typeof(InMemDriver), "_data", driver);
        }

        public static void SetBuffer(this InMemDriver driver, byte[] buffer)
        {
            Utils.SetPrivateField(typeof(InMemDriver), "_data", driver, buffer);
        }

        public static long GetNumTicks(this CampaignTime time)
        {
            return m_GetNumTicks.Invoke(time);
        }

        public static CampaignTime CreateCampaignTime(long numTicks)
        {
            ConstructorInfo ctorCampaignTime = typeof(CampaignTime).Assembly
                                                                   .GetType("TaleWorlds.CampaignSystem.CampaignTime")
                                                                   .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];

            return (CampaignTime)ctorCampaignTime.Invoke(new object[] { numTicks });
        }

        private static Func<CampaignTime, long> m_GetNumTicks = InvokableFactory.CreateGetter<CampaignTime, long>(
            typeof(CampaignTime).GetField("_numTicks", 
                BindingFlags.NonPublic | BindingFlags.Instance));
    }
}
