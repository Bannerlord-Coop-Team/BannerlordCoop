using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncFieldBuilder : DynamicSyncBuilderBase
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncFieldBuilder(IObjectManager objectManager, DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
        {
            this.objectManager = objectManager;
        }
        public string GetTranspiler(FieldInfo fieldInfo)
        {
            return GetSetTranspiler(fieldInfo);
        }

        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(fieldInfo);
            string networkMessage;
            if (RuntimeTypeModel.Default.CanSerialize(fieldInfo.FieldType))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            yield return localMessage;
            yield return networkMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            if (RuntimeTypeModel.Default.CanSerialize(fieldInfo.FieldType))
                return TemplateParser.Parse("Handlers.SubscribeSetValueTemplate", templateData);
            else
                return TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", templateData);
        }

        private object GetTemplateData(FieldInfo fieldInfo)
        {
            var serializerNames = GetSerializerMethodNames(fieldInfo.FieldType);
            return new
            {
                MemberDeclaringType = fieldInfo.DeclaringType.Name,
                MemberName = fieldInfo.Name,
                MemberType = fieldInfo.FieldType.Name,
                Libraries = DynamicSyncUtils.GetLibraries(fieldInfo),
                SerializeMethod = serializerNames.serialize,
                DeserializeMethod = serializerNames.deserialize,
                ReadOnly = fieldInfo.IsInitOnly,
                ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null
            };
        }
    }
}
