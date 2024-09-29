using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CraftingTemplates
{
    internal class CraftingTemplateRegistry : RegistryBase<CraftingTemplate>
    {
        private const string TemplateStringIdPrefix = "CoopCraftingTemplate";

        public CraftingTemplateRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var objectManager = MBObjectManager.Instance;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var template in objectManager.GetObjectTypeList<CraftingTemplate>())
            {
                RegisterExistingObject(template.StringId, template);
            }
        }

        protected override string GetNewId(CraftingTemplate template)
        {
            template.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<CraftingTemplate>(TemplateStringIdPrefix);
            return template.StringId;
        }
    }
}
