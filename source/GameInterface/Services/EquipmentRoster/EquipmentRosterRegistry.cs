using GameInterface.Registry;
using System.Linq;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.EquipmentRoster
{
    internal class EquipmentRosterRegistry : RegistryBase<MBEquipmentRoster>
    {
        private const string IdPrefix = "CoopEquipmentRoster";
        private static int InstanceCounter = 0;

        public EquipmentRosterRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var equipRoster in objectManager.GetObjectTypeList<MBEquipmentRoster>())
            {
                RegisterExistingObject(equipRoster.StringId, equipRoster);
            }
        }

        protected override string GetNewId(MBEquipmentRoster obj)
        {
            return $"{IdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
