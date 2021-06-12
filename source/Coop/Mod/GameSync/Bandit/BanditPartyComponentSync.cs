using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.GameSync.Bandit
{
    class BanditPartyComponentSync : CoopManaged<BanditPartyComponentSync, BanditPartyComponent>
    {
        static BanditPartyComponentSync()
        {
            //When(GameLoop)
            //    .Calls(Method(nameof(BanditPartyComponent.CreateBanditParty)))
            //    .DelegateTo(IsServer);

            ApplyStaticPatches();
            AutoWrapAllInstances(c => new BanditPartyComponentSync(c));
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        public BanditPartyComponentSync([NotNull] BanditPartyComponent instance) : base(instance)
        {
        }
    }
}
