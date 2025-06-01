using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyArrayBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyArrayBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }
        public string GetPrefix(PropertyInfo propertyInfo) => DynamicSyncUtils.GetPrefix(propertyInfo);

        public string GetTranspiler(PropertyInfo propertyInfo)
        {
            return TemplateParser.Parse("Patches.PropertyArrayChangeTranspilerTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        ElementType = propertyInfo.PropertyType.GetElementType().Name,
                        Libraries = DynamicSyncUtils.GetLibraries(propertyInfo)
                    });
        }


        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            var templateData = GetTemplateData(propertyInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(propertyInfo);

            string localChangeMessage = TemplateParser.Parse("Messages.LocalArrayChangeMessageTemplate", templateData);

            string networkMessage;
            string networkChangeMessage;
            if(objectManager.IsTypeManaged(propertyInfo.PropertyType.GetElementType()))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetReferenceMessageTemplate", templateData);
                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeReferenceMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetValueMessageTemplate", templateData);
                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeValueMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_ArrayChangeLocalMessage.cs", localChangeMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_ArrayChangeNetworkMessage.cs", networkChangeMessage);

            yield return localMessage;
            yield return localChangeMessage;
            yield return networkMessage;
            yield return networkChangeMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            var templateData = GetTemplateData(propertyInfo);
            if (objectManager.IsTypeManaged(propertyInfo.PropertyType.GetElementType()))
            {
                return TemplateParser.Parse("Handlers.SubscribeArrayReferenceTemplate", templateData);
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeArrayValueTemplate", templateData);
            }
        }
        private string GetArrayType(Type type)
        {
            return type.GetElementType().Name + "[]";
        }

        private object GetTemplateData(PropertyInfo propertyInfo)
        {
            return new
            {
                MemberDeclaringType = propertyInfo.DeclaringType.Name,
                MemberName = propertyInfo.Name,
                MemberType = GetArrayType(propertyInfo.PropertyType),
                ElementType = propertyInfo.PropertyType.GetElementType().Name,
                Libraries = DynamicSyncUtils.GetLibraries(propertyInfo),
                NotReadOnly = propertyInfo.SetMethod != null
            };
        }
    }
}
