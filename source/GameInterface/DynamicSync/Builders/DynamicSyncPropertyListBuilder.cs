using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyListBuilder : DynamicSyncBuilderBase
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyListBuilder(IObjectManager objectManager, DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
        {
            this.objectManager = objectManager;
        }
        public string GetPrefix(PropertyInfo propertyInfo) => DynamicSyncUtils.GetPrefix(propertyInfo);


        public string GetTranspiler(PropertyInfo propertyInfo)
        {
            string changeTemplate = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", GetTemplateData(propertyInfo));

            return changeTemplate;
        }


        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            var templateData = GetTemplateData(propertyInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(propertyInfo);

            string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate", templateData);
            string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);

            string networkMessage;
            string networkAddMessage;
            string networkRemoveMessage;
            if (objectManager.IsTypeManaged(GetElementType(propertyInfo.PropertyType)))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetReferenceMessageTemplate", templateData);
                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddReferenceMessageTemplate", templateData);
                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveReferenceMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetValueMessageTemplate", templateData);
                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddValueMessageTemplate", templateData);
                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveValueMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddLocalMessage.cs", localAddMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

            yield return localMessage;
            yield return localAddMessage;
            yield return localRemoveMessage;
            yield return networkMessage;
            yield return networkAddMessage;
            yield return networkRemoveMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            var templateData = GetTemplateData(propertyInfo);
            if (objectManager.IsTypeManaged(GetElementType(propertyInfo.PropertyType)))
            {
                return TemplateParser.Parse("Handlers.SubscribeCollectionReferenceTemplate", templateData);
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeCollectionValueTemplate", templateData);
            }
        }
        private string GetListTypeName(Type type)
        {
            return $"List<{type.GetGenericArguments()[0].Name}>";
        }

        private Type GetElementType(Type type)
        {
            return type.GetGenericArguments()[0];
        }

        private object GetTemplateData(PropertyInfo propertyInfo)
        {
            var serializers = GetSerializerMethodNames(GetElementType(propertyInfo.PropertyType));
            return new
            {
                MemberDeclaringType = propertyInfo.DeclaringType.Name,
                MemberName = propertyInfo.Name,
                MemberType = GetListTypeName(propertyInfo.PropertyType),
                ElementType = GetElementType(propertyInfo.PropertyType).Name,
                Libraries = DynamicSyncUtils.GetLibraries(propertyInfo),
                NotReadOnly = propertyInfo.SetMethod != null,
                SerializeMethod = serializers.serialize,
                DeserializeMethod = serializers.deserialize
            };
        }
    }
}
