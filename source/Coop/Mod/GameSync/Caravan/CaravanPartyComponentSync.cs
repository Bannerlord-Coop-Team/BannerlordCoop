using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace Coop.Mod.GameSync.Caravan
{
    class CaravanPartyComponentSync : CoopManaged<CaravanPartyComponentSync, CaravanPartyComponent>
    {
        static CaravanPartyComponentSync()
        {
            //When(GameLoop)
            //    .Calls(Method(nameof(CaravanPartyComponent.CreateCaravanParty)),
            //           Method(typeof(HeroCreator), nameof(HeroCreator.CreateSpecialHero)))
            //    .DelegateTo(IsServer);

            ApplyStaticPatches();
            AutoWrapAllInstances(c => new CaravanPartyComponentSync(c));
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        public CaravanPartyComponentSync([NotNull] CaravanPartyComponent instance) : base(instance)
        {
        }
    }
}
