using System;
using System.Linq;
using Coop.Mod.Persistence;
using CoopFramework;
using HarmonyLib;
using Mono.Reflection;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeControl
    {
        private class TPatch
        {
            // TODO: Replace TimeControl with a CoopManaged
        }
        private static readonly PropertyPatch<TPatch> TimeControlPatch =
            new PropertyPatch<TPatch>(typeof(Campaign)).InterceptSetter(nameof(Campaign.TimeControlMode));

        private static readonly PropertyPatch<TPatch> TimeControlLockPatch =
            new PropertyPatch<TPatch>(typeof(Campaign)).InterceptSetter(
                nameof(Campaign.TimeControlModeLock));

        private static readonly PropertyPatch<TPatch> IsMainPartyWaitingPatch =
            new PropertyPatch<TPatch>(typeof(Campaign)).InterceptSetter(
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
        public static void Init(ISynchronization sync)
        {
            sync.RegisterSyncedField(TimeControlMode, TimeControlPatch.Setters, DoSyncTimeControl);
            sync.RegisterSyncedField(
                TimeControlModeLock,
                TimeControlLockPatch.Setters,
                DoSyncTimeControl);

            MethodAccess mainPartyWaitingSetter = IsMainPartyWaitingPatch.Setters.First();
            mainPartyWaitingSetter.ConditionIsPatchActive = o => Coop.DoSync();
            mainPartyWaitingSetter.Prefix.SetGlobalHandler(SetIsMainPartyWaiting);
        }

        private static ECallPropagation SetIsMainPartyWaiting(object instance, object[] args)
        {
            IEnvironmentClient env = CoopClient.Instance?.Persistence?.Environment;
            if (env == null) return ECallPropagation.CallOriginal;
            if (args.Length != 1) throw new ArgumentException();
            if (!(args[0] is bool isLocalMainPartyWaiting)) throw new ArgumentException();
            if (!(instance is Campaign campaign)) throw new ArgumentException();

            bool isEveryMainPartyWaiting = isLocalMainPartyWaiting;
            foreach (MobileParty party in env.PlayerControlledParties)
            {
                isEveryMainPartyWaiting = isEveryMainPartyWaiting && party.ComputeIsWaiting();
            }

            IsMainPartyWaitingPatch
                .Setters.First()
                .Call(ETriggerOrigin.Authoritative, instance, new object[] { isEveryMainPartyWaiting });
            return ECallPropagation.Suppress;
        }

        public static bool DoSyncTimeControl()
        {
            return Coop.DoSync() && CanSyncTimeControlMode;
        }
    }
}
