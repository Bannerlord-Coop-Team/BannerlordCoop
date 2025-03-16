using GameInterface.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Workshops
{
    internal class WorkshopRegistry : RegistryBase<Workshop>
    {
        private const string WorkshopIdPrefix = $"Coop{nameof(Workshop)}";
        private int InstanceCounter = 0;

        public WorkshopRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (Town town in Town.AllTowns)
            {
                int counter = 1;

                foreach (Workshop workshop in town.Workshops)
                {
                    var networkId = $"{nameof(Workshop)}_{town.StringId}_{counter++}";
                    RegisterExistingObject(networkId, workshop);
                }
            }
        }

        protected override string GetNewId(Workshop shop)
        {
            return $"{WorkshopIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}