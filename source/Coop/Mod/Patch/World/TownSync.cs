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
    class TownSync : CoopManaged<TownSync, Town>
    {
        static TownSync()
        {
            When(GameLoop)
                .Calls(Setter(nameof(Town.Loyalty)))
                .Broadcast(() => CoopClient.Instance.Synchronization)
                .DelegateTo(IsServer);

            When(GameLoop)
                .Calls(Setter(nameof(Town.Security)))
                .Broadcast(() => CoopClient.Instance.Synchronization)
                .DelegateTo(IsServer);

            When(GameLoop)
                .Calls(Method("DesertOneTroopFromGarrison"))
                .Broadcast(() => CoopClient.Instance.Synchronization)
                .DelegateTo(IsServer);

            AutoWrapAllInstances(c => new TownSync(c));
        }

        public TownSync([NotNull] Town instance) : base(instance)
        {
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }
    }

    class FiefSync : CoopManaged<FiefSync, Fief>
    {
        static FiefSync()
        {
            When(GameLoop)
                .Calls(Setter(nameof(Fief.FoodStocks)))
                .Broadcast(() => CoopClient.Instance.Synchronization)
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
    }
}
