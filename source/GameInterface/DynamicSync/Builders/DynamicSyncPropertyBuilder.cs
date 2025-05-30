using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }
        public string GetPrefix(PropertyInfo propertyInfo) => DynamicSyncUtils.GetPrefix(propertyInfo);

        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(propertyInfo);

            string networkMessage;
            if(objectManager.IsTypeManaged(propertyInfo.PropertyType))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = propertyInfo.PropertyType.Name,
                        Libraries = DynamicSyncUtils.GetLibraries(propertyInfo)
                    });
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = propertyInfo.PropertyType.Name,
                        Libraries = DynamicSyncUtils.GetLibraries(propertyInfo)
                    });
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            yield return localMessage;
            yield return networkMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            if (objectManager.IsTypeManaged(propertyInfo.PropertyType))
            {
                return TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = propertyInfo.PropertyType.Name,
                        Libraries = DynamicSyncUtils.GetLibraries(propertyInfo)
                    });
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeSetValueTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = propertyInfo.PropertyType.Name,
                        Libraries = DynamicSyncUtils.GetLibraries(propertyInfo)
                    });
            }
        }
    }
}
