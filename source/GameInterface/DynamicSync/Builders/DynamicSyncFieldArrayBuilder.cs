using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncFieldArrayBuilder : DynamicSyncBuilderBase
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncFieldArrayBuilder(IObjectManager objectManager, DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
        {
            this.objectManager = objectManager;
        }

        public string GetTranspiler(FieldInfo fieldInfo)
        {
            string setTemplate = GetSetTranspiler(fieldInfo);

            string changeTemplate = TemplateParser.Parse("Patches.FieldArrayChangeTranspilerTemplate", GetTemplateData(fieldInfo));

            return string.Join(Environment.NewLine, setTemplate, changeTemplate);
        }


        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(fieldInfo);

            string localChangeMessage = TemplateParser.Parse("Messages.LocalArrayChangeMessageTemplate", templateData);

            string networkMessage;
            string networkChangeMessage;
            if(objectManager.IsTypeManaged(fieldInfo.FieldType.GetElementType()))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetReferenceMessageTemplate", templateData);
                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeReferenceMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetValueMessageTemplate", templateData);
                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeValueMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_ArrayChangeLocalMessage.cs", localChangeMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_ArrayChangeNetworkMessage.cs", networkChangeMessage);

            yield return localMessage;
            yield return localChangeMessage;
            yield return networkMessage;
            yield return networkChangeMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            if (objectManager.IsTypeManaged(fieldInfo.FieldType.GetElementType()))
                return TemplateParser.Parse("Handlers.SubscribeArrayReferenceTemplate", templateData);
            else
                return TemplateParser.Parse("Handlers.SubscribeArrayValueTemplate", templateData);
        }
        private string GetArrayType(Type type)
        {
            return type.GetElementType().Name + "[]";
        }
        private object GetTemplateData(FieldInfo fieldInfo)
        {
            var serializers = GetSerializerMethodNames(fieldInfo.FieldType.GetElementType());
            return new
            {
                MemberDeclaringType = fieldInfo.DeclaringType.Name,
                MemberName = fieldInfo.Name,
                MemberType = GetArrayType(fieldInfo.FieldType),
                ElementType = fieldInfo.FieldType.GetElementType().Name,
                Libraries = DynamicSyncUtils.GetLibraries(fieldInfo),
                SerializeMethod = serializers.serialize,
                DeserializeMethod = serializers.deserialize,
                ReadOnly = fieldInfo.IsInitOnly,
                ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null
            };
        }
    }
}
