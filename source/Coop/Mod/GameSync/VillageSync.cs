using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Mod.Patch.World
{
    class VillageSync : CoopManaged<VillageSync, Village>
    {
        static VillageSync()
        {
            AutoWrapAllInstances(c => new VillageSync(c));
        }

        public VillageSync([NotNull] Village instance) : base(instance)
        {
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }
    }
}
