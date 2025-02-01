using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.BasicCultureObjects
{
    internal class BasicCultureObjectRegistry : RegistryBase<BasicCultureObject>
    {
        private const string IdPrefix = "CoopBasicCulture";
        private static int InstanceCounter = 0;

        public BasicCultureObjectRegistry(IRegistryCollection collection) : base(collection)
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

            foreach (var culture in objectManager.GetObjectTypeList<BasicCultureObject>())
            {
                RegisterExistingObject(culture.StringId, culture);
            }
        }

        protected override string GetNewId(BasicCultureObject obj)
        {
            return $"{IdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
