﻿using System;
using System.Linq;
using Coop.Mod.Persistence;
using HarmonyLib;
using Mono.Reflection;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeControl
    {
        private static readonly PropertyPatch TimeControlPatch =
            new PropertyPatch(typeof(Campaign)).InterceptSetter(nameof(Campaign.TimeControlMode));

        private static readonly PropertyPatch TimeControlLockPatch =
            new PropertyPatch(typeof(Campaign)).InterceptSetter(
                nameof(Campaign.TimeControlModeLock));

        private static readonly PropertyPatch IsMainPartyWaitingPatch =
            new PropertyPatch(typeof(Campaign), EPatchBehaviour.NeverCallOriginal).InterceptSetter(
                nameof(Campaign.IsMainPartyWaiting));

        public static FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; } =
            new FieldAccess<Campaign, CampaignTimeControlMode>(
                AccessTools.Property(typeof(Campaign), nameof(Campaign.TimeControlMode))
                           .GetBackingField());

        public static FieldAccess<Campaign, bool> TimeControlModeLock { get; } =
            new FieldAccess<Campaign, bool>(
                AccessTools.Property(typeof(Campaign), nameof(Campaign.TimeControlModeLock))
                           .GetBackingField());

        public static bool CanSyncTimeControlMode = false;

        [PatchInitializer]
        public static void Init()
        {
            FieldChangeBuffer.Intercept(TimeControlMode, TimeControlPatch.Setters, DoSyncTimeControl);
            FieldChangeBuffer.Intercept(
                TimeControlModeLock,
                TimeControlLockPatch.Setters,
                DoSyncTimeControl);

            MethodAccess mainPartyWaitingSetter = IsMainPartyWaitingPatch.Setters.First();
            mainPartyWaitingSetter.Condition = o => Coop.DoSync();
            mainPartyWaitingSetter.SetGlobalHandler(SetIsMainPartyWaiting);
        }

        private static void SetIsMainPartyWaiting(object instance, object value)
        {
            IEnvironmentClient env = CoopClient.Instance?.Persistence?.Environment;
            if (env == null) return;
            if (!(value is object[] args)) throw new ArgumentException();
            if (!(args[0] is bool isLocalMainPartyWaiting)) throw new ArgumentException();
            if (!(instance is Campaign campaign)) throw new ArgumentException();

            bool isEveryMainPartyWaiting = isLocalMainPartyWaiting;
            foreach (MobileParty party in env.PlayerControlledParties)
            {
                isEveryMainPartyWaiting = isEveryMainPartyWaiting && party.ComputeIsWaiting();
            }

            IsMainPartyWaitingPatch
                .Setters.First()
                .CallOriginal(instance, new object[] { isEveryMainPartyWaiting });
        }

        public static bool DoSyncTimeControl()
        {
            return Coop.DoSync() && CanSyncTimeControlMode;
        }
    }
}
