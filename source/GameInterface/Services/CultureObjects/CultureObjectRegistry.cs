using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.BasicCultureObjects
{
    internal class CultureObjectRegistry : RegistryBase<CultureObject>
    {
        private const string CultureStringIdPrefix = "CoopCulture";

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
            culture.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<CultureObject>(CultureStringIdPrefix);
            return culture.StringId;
        }
    }
}
