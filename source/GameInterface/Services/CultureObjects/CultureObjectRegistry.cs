using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects
{
    internal class CultureObjectRegistry : RegistryBase<CultureObject>
    {
        private const string CultureStringIdPrefix = "CoopCulture";
        private static int ObjectCounter = 0;

        public CultureObjectRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var culture in objectManager.GetObjectTypeList<CultureObject>())
            {
                RegisterExistingObject(culture.StringId, culture);
            }
        }

        protected override string GetNewId(CultureObject culture)
        {
            return $"{CultureStringIdPrefix}_{Interlocked.Increment(ref ObjectCounter)}";
        }
    }
}
