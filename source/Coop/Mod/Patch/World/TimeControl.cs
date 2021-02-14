using System;
using CoopFramework;
using JetBrains.Annotations;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.World
{
    public class TimeControl : CoopManaged<TimeControl, Campaign>
    {
        public static bool CanSyncTimeControlMode = false;
        static TimeControl()
        {
            When(GameLoop)
                .Calls(Setter(nameof(Campaign.TimeControlMode)), Setter(nameof(Campaign.TimeControlModeLock)))
                .Broadcast(() => CoopClient.Instance.Synchronization, new CanChangeTimeValidator())
                .Skip();
            When(GameLoop)
                .Calls(Setter(nameof(Campaign.IsMainPartyWaiting)))
                .DelegateTo(SetIsMainPartyWaiting);
            AutoWrapAllInstances(c => new TimeControl(c));
        }
        
        private class CanChangeTimeValidator : IActionValidator
        {
            public EValidationResult Validate()
            {
                return CanSyncTimeControlMode ? EValidationResult.Valid : EValidationResult.Invalid;
            }
        }
        public TimeControl([NotNull] Campaign instance) : base(instance)
        {
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
            Registry.IdToMethod[call.Id].CallOriginal(call.Instance, call.Parameters);
            return ECallPropagation.Skip;
        }
    }
}
