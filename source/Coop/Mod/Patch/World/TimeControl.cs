using System;
using Coop.Mod.Persistence;
using CoopFramework;
using JetBrains.Annotations;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod.Patch.World
{
    /// <summary>
    ///     Patches the time control in the local campaign instance and synchronizes it across all players.
    /// </summary>
    public class TimeControl : CoopManaged<TimeControl, Campaign>
    {
        public static bool CanSyncTimeControlMode = false;
        static TimeControl()
        {
            When(GameLoop)
                .Calls(Setter(nameof(Campaign.TimeControlMode)), Setter(nameof(Campaign.TimeControlModeLock)))
                .Broadcast(() => CoopClient.Instance.Synchronization, new CanChangeTimeServerside())
                .DelegateTo(IsServerOrAllClientsPlaying);

            When(GameLoop)
                .Calls(Setter(nameof(Campaign.IsMainPartyWaiting)))
                .DelegateTo(SetIsMainPartyWaiting);
            AutoWrapAllInstances(c => new TimeControl(c));
        }
        
        /// <summary>
        ///     Serverside check if the time control mode can be changed right now.
        /// </summary>
        private class CanChangeTimeServerside : IActionValidator
        {
            public bool IsAllowed()
            {
                return CoopServer.Instance.AreAllClientsPlaying;
            }

            public string GetReasonForRejection()
            {
                return "Some players are currently connecting";
            }
        }
        public TimeControl([NotNull] Campaign instance) : base(instance)
        {
        }

        private static ECallPropagation IsServerOrAllClientsPlaying(IPendingMethodCall call)
        {
            return Coop.IsServer || CoopServer.Instance.AreAllClientsPlaying ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        private static ECallPropagation SetIsMainPartyWaiting(IPendingMethodCall call)
        {
            IEnvironmentClient env = CoopClient.Instance?.Persistence?.Environment;
            var args = call.Parameters;
            if (env == null) return ECallPropagation.CallOriginal;
            if (args.Length != 1) throw new ArgumentException();
            if (!(args[0] is bool isLocalMainPartyWaiting)) throw new ArgumentException();
            if (!(call.Instance is Campaign campaign)) throw new ArgumentException();

            bool isEveryMainPartyWaiting = isLocalMainPartyWaiting;
            foreach (MobileParty party in env.PlayerMainParties)
            {
                isEveryMainPartyWaiting = isEveryMainPartyWaiting && party.ComputeIsWaiting();
            }

            // Override
            return isEveryMainPartyWaiting ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }
        
        private static readonly Condition CanChangeTimeClientside = new Condition((eOrigin, _) => CanSyncTimeControlMode);
    }
}
