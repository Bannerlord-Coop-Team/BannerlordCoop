using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace Coop.Mod.GameSync.Villager
{
    class VillagerPartyComponentSync : CoopManaged<VillagerPartyComponentSync, VillagerPartyComponent>
    {
        static VillagerPartyComponentSync()
        {
            //When(GameLoop)
            //    .Calls(Method(nameof(VillagerPartyComponent.CreateVillagerParty)))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            ApplyStaticPatches();
            AutoWrapAllInstances(c => new VillagerPartyComponentSync(c));
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        public VillagerPartyComponentSync([NotNull] VillagerPartyComponent instance) : base(instance)
        {
        }
    }
}
