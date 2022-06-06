using Coop.Mod.Persistence;
using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Mod.Patch.World
{
    internal class SettlementSync : CoopManaged<SettlementSync, Settlement>
    {
        static SettlementSync()
        {
            //When(GameLoop)
            //    .Calls(Setter(nameof(Settlement.Prosperity)))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method("AddMobileParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsClientParty);

            //When(GameLoop)
            //    .Calls(Method("FinalizeSiegeEvent"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method(nameof(Settlement.InitializeSiegeEventSide)))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .Execute();

            //When(GameLoop)
            //    .Calls(Method("RemoveHeroWithoutParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method("RemoveMobileParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsClientParty);

            //When(GameLoop)
            //    .Calls(Method("SetNextSiegeState"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method(nameof(Settlement.SetWallSectionHitPointsRatioAtIndex)))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method("SpawnMilitiaParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method("TransferReadyMilitiasToMilitiaParty"))
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method("AddMilitiasToParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            //When(GameLoop)
            //    .Calls(Method("RemoveMilitiasFromParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);            

            AutoWrapAllInstances(c => new SettlementSync(c));

            //When(GameLoop)
            //    .Calls(Method(typeof(EnterSettlementAction), "ApplyInternal"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsClientParty);

            //When(GameLoop)
            //    .Calls(Method(typeof(LeaveSettlementAction), "ApplyForParty"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsClientParty);

            ApplyStaticPatches();
        }

        public SettlementSync([NotNull] Settlement instance) : base(instance)
        {
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        private static ECallPropagation IsClientParty(IPendingMethodCall call)
        {

            MobileParty party = call.Parameters.FirstOrDefault(x => x?.GetType() == typeof(MobileParty)) as MobileParty;

            IEnvironmentClient env = CoopClient.Instance?.Persistence?.Environment;

            if (env == null) return ECallPropagation.Skip;

            bool isMainParty = call.Parameters.Contains(MobileParty.MainParty) && CoopClient.Instance.ClientPlaying;
            if (Coop.IsServer || isMainParty)
            {
                return ECallPropagation.CallOriginal;
            }
            return ECallPropagation.Skip;
        }
    }
}
