using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch.World
{
    class SettlementSync : CoopManaged<SettlementSync, Settlement>
    {
        static SettlementSync()
        {
            When(GameLoop)
                .Calls(Method(nameof(Settlement.SetWallSectionHitPointsRatioAtIndex)))
                .Broadcast(() => CoopClient.Instance.Synchronization)
                .DelegateTo(IsServer);

            When(GameLoop)
                .Calls(Setter(nameof(Settlement.Prosperity)))
                .Broadcast(() => CoopClient.Instance.Synchronization)
                .DelegateTo(IsServer);

            AutoWrapAllInstances(c => new SettlementSync(c));
        }

        public SettlementSync([NotNull] Settlement instance) : base(instance)
        {
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }
    }
}
