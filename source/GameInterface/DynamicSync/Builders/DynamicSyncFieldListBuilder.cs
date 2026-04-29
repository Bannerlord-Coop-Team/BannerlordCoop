using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncFieldListBuilder : DynamicSyncBuilderBase
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncFieldListBuilder(IObjectManager objectManager, DynamicSyncRegistry dynamicSyncRegistry) : base(dynamicSyncRegistry)
        {
            this.objectManager = objectManager;
        }

        public string GetTranspiler(FieldInfo fieldInfo)
        {
            string setTemplate = DynamicSyncUtils.GetSetTranspiler(fieldInfo);
            string changeTemplate = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", GetTemplateData(fieldInfo));
            return string.Join(Environment.NewLine, setTemplate, changeTemplate);
        }


        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(fieldInfo);

            string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate", templateData);
            string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);

            string networkMessage;
            string networkAddMessage;
            string networkRemoveMessage;
            if (RuntimeTypeModel.Default.CanSerialize(GetElementType(fieldInfo.FieldType)))
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

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddLocalMessage.cs", localAddMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

            yield return localMessage;
            yield return localAddMessage;
            yield return localRemoveMessage;
            yield return networkMessage;
            yield return networkAddMessage;
            yield return networkRemoveMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            if (RuntimeTypeModel.Default.CanSerialize(GetElementType(fieldInfo.FieldType)))
                return TemplateParser.Parse("Handlers.SubscribeCollectionValueTemplate", templateData);
            else
                return TemplateParser.Parse("Handlers.SubscribeCollectionReferenceTemplate", templateData);
        }
        private string GetListTypeName(Type type)
        {
            return $"List<{type.GetGenericArguments()[0].Name}>";
        }

        private Type GetElementType(Type type)
        {
            return type.GetGenericArguments()[0];
        }

        private object GetTemplateData(FieldInfo fieldInfo)
        {
            var serializers = GetSerializerMethodNames(GetElementType(fieldInfo.FieldType));
            return new
            {
                MemberDeclaringType = fieldInfo.DeclaringType.Name,
                MemberName = fieldInfo.Name,
                MemberType = GetListTypeName(fieldInfo.FieldType),
                ElementType = GetElementType(fieldInfo.FieldType).Name,
                Libraries = DynamicSyncUtils.GetLibraries(fieldInfo),
                SerializeMethod = serializers.serialize,
                DeserializeMethod = serializers.deserialize,
                ReadOnly = fieldInfo.IsInitOnly,
                ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null
            };
        }
    }
}
