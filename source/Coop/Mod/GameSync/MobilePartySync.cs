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
    class MobilePartySync : CoopManaged<MobilePartySync, MobileParty>
    {
        static MobilePartySync()
        {
            //When(GameLoop)
            //    .Calls(Method(nameof(MobileParty.CreateParty)))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            ApplyStaticPatches();
            AutoWrapAllInstances(c => new MobilePartySync(c));
        }

        public MobilePartySync([NotNull] MobileParty instance) : base(instance)
        {
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }
    }
}
