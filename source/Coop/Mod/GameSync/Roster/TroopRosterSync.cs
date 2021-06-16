using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.GameSync.Roster
{
    class TroopRosterSync : CoopManaged<TroopRosterSync, TroopRoster>
    {
        static TroopRosterSync()
        {
            //When(GameLoop)
            //    .Calls(Method(nameof(TroopRoster.AddToCountsAtIndex)),
            //           Method("AddNewElement"))
            //    .Broadcast(() => CoopClient.Instance.Synchronization)
            //    .DelegateTo(IsServer);

            ApplyStaticPatches();
            AutoWrapAllInstances(c => new TroopRosterSync(c));
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            return Coop.IsServer ? ECallPropagation.CallOriginal : ECallPropagation.Skip;
        }

        public TroopRosterSync([NotNull] TroopRoster instance) : base(instance)
        {
        }
    }
}
