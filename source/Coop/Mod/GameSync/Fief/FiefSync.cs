using Common;
using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.GameSync
{
    class FiefSync : CoopManaged<FiefSync, Fief>
    {
        static FiefSync()
        {
            When(GameLoop)
                .Calls(Setter(nameof(Fief.FoodStocks)))
                .Broadcast(() => CoopClient.Instance.Synchronization, authority: EAuthority.ServerAuthorityOnly)
                .DelegateTo(IsServer);

            AutoWrapAllInstances(c => new FiefSync(c));
        }

        public FiefSync([NotNull] Fief instance) : base(instance)
        {
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        private class UpdateAllowed : IActionValidator
        {
            public bool IsAllowed()
            {
                return Coop.IsServer;
            }

            public string GetReasonForRejection()
            {
                return "Not Authorized";
            }
        }

        #region Utils
        public static FiefSync MakeManaged(Fief fief)
        {
            if (Instances.TryGetValue(fief, out FiefSync instance))
            {
                return instance;
            }
            return new FiefSync(fief);
        }
        #endregion
    }
}
