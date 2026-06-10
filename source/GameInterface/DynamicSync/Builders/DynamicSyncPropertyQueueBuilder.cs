using GameInterface.DynamicSync.Templates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyQueueBuilder : DynamicSyncBuilderBase
    {
        public DynamicSyncPropertyQueueBuilder(
            DynamicSyncRegistry dynamicSyncRegistry,
            DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
        {
        }

        public string GetPrefix(Debuggable<PropertyInfo> propertyItem) => DynamicSyncUtils.GetPrefix(propertyItem);


        public IEnumerable<string> GetTranspilers(Debuggable<PropertyInfo> propertyItem)
        {
            yield return TemplateParser.Parse("Patches.PropertyQueueChangeTranspilerTemplate", GetTemplateData(propertyItem));
            yield return TemplateParser.Parse("Patches.QueueClearTranspilerTemplate", GetTemplateData(propertyItem));
        }


        public IEnumerable<string> GetMessages(Debuggable<PropertyInfo> propertyItem)
        {
            var propertyInfo = propertyItem.Value;

            var templateData = GetTemplateData(propertyItem);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(propertyInfo);

            string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate", templateData);
            string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);
            string localClearMessage = TemplateParser.Parse("Messages.LocalQueueClearMessageTemplate", templateData);
            string networkClearMessage = TemplateParser.Parse("Messages.NetworkQueueClearMessageTemplate", templateData);

            string networkMessage;
            string networkAddMessage;
            string networkRemoveMessage;
            if (RuntimeTypeModel.Default.CanSerialize(GetElementType(propertyInfo.PropertyType)))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetValueMessageTemplate", templateData);
                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddValueMessageTemplate", templateData);
                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveValueMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetReferenceMessageTemplate", templateData);
                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddReferenceMessageTemplate", templateData);
                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveReferenceMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddLocalMessage.cs", localAddMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/Local_{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_QueueClear.cs", localClearMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/Network_{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_QueueClear.cs", networkClearMessage);

            yield return localMessage;
            yield return localAddMessage;
            yield return localRemoveMessage;
            yield return localClearMessage;
            yield return networkMessage;
            yield return networkAddMessage;
            yield return networkRemoveMessage;
            yield return networkClearMessage;
        }

        public IEnumerable<string> GetSubscriptions(Debuggable<PropertyInfo> propertyItem)
        {
            var propertyInfo = propertyItem.Value;

            var templateData = GetTemplateData(propertyItem);
            if (RuntimeTypeModel.Default.CanSerialize(GetElementType(propertyInfo.PropertyType)))
            {
                yield return TemplateParser.Parse("Handlers.SubscribeQueueValueTemplate", templateData);
            }
            else
            {
                yield return TemplateParser.Parse("Handlers.SubscribeQueueReferenceTemplate", templateData);
            }

            yield return TemplateParser.Parse("Handlers.SubscribeQueueClearTemplate", templateData);
        }

        private string GetQueueTypeName(Type type)
        {
            return $"Queue<{type.GetGenericArguments()[0].Name}>";
        }

        private Type GetElementType(Type type)
        {
            return type.GetGenericArguments()[0];
        }

        private object GetTemplateData(Debuggable<PropertyInfo> propertyItem)
        {
            var propertyInfo = propertyItem.Value;

            var serializers = GetSerializerMethodNames(GetElementType(propertyInfo.PropertyType));
            return new
            {
                MemberDeclaringType = propertyInfo.DeclaringType.Name,
                MemberName = propertyInfo.Name,
                MemberType = GetQueueTypeName(propertyInfo.PropertyType),
                ElementType = GetElementType(propertyInfo.PropertyType).Name,
                Libraries = DynamicSyncUtils.GetLibraries(propertyInfo),
                NotReadOnly = propertyInfo.SetMethod != null,
                SerializeMethod = serializers.serialize,
                DeserializeMethod = serializers.deserialize,
                Debug = propertyItem.Debug
            };
        }
    }
}
