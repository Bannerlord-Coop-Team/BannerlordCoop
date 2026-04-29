using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyBuilder : DynamicSyncBuilderBase
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyBuilder(IObjectManager objectManager, DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
        {
            this.objectManager = objectManager;
        }
        public string GetPrefix(PropertyInfo propertyInfo) => DynamicSyncUtils.GetPrefix(propertyInfo);

        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            var templateData = GetTemplateData(propertyInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(propertyInfo);
            string networkMessage;
            if (RuntimeTypeModel.Default.CanSerialize(propertyInfo.PropertyType))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            yield return localMessage;
            yield return networkMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            var templateData = GetTemplateData(propertyInfo);
            if (RuntimeTypeModel.Default.CanSerialize(propertyInfo.PropertyType))
                return TemplateParser.Parse("Handlers.SubscribeSetValueTemplate", templateData);
            else
                return TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", templateData);
        }

        private object GetTemplateData(PropertyInfo propertyInfo)
        {
            var serializerNames = GetSerializerMethodNames(propertyInfo.PropertyType);
            return new
            {
                MemberDeclaringType = propertyInfo.DeclaringType.Name,
                MemberName = propertyInfo.Name,
                MemberType = propertyInfo.PropertyType.Name,
                Libraries = DynamicSyncUtils.GetLibraries(propertyInfo),
                SerializeMethod = serializerNames.serialize,
                DeserializeMethod = serializerNames.deserialize,
                Interface = propertyInfo.PropertyType.IsInterface
            };
        }
    }
}
