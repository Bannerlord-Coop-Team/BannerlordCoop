using GameInterface.DynamicSync.Templates;
using ProtoBuf.Meta;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncFieldBuilder : DynamicSyncBuilderBase
{
    public DynamicSyncFieldBuilder(
        DynamicSyncRegistry dynamicSyncRegistry,
        DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
    {
    }
    public string GetTranspiler(Debuggable<FieldInfo> fieldInfo)
    {
        return GetSetTranspiler(fieldInfo);
    }

    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldInfo, fieldItem.Debug);
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

    public string GetSubscription(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldInfo, fieldItem.Debug);
        if (RuntimeTypeModel.Default.CanSerialize(fieldInfo.FieldType))
            return TemplateParser.Parse("Handlers.SubscribeSetValueTemplate", templateData);
        else
            return TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", templateData);
    }

    private object GetTemplateData(FieldInfo fieldInfo, bool debug)
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
            ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            Debug = debug
        };
    }
}
