using GameInterface.AutoSync.Templates;
using ProtoBuf.Meta;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldBuilder : AutoSyncBuilderBase
{
    public AutoSyncFieldBuilder(
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder)
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
        string localMessage = AutoSyncUtils.GetLocalSetMessage(fieldInfo);
        string networkMessage;
        if (RuntimeTypeModel.Default.CanSerialize(fieldInfo.FieldType))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
        }

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

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
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType),
            MemberDeclaringTypeName = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType).Replace(".", "_"),
            MemberName = fieldInfo.Name,
            MemberType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.FieldType),
            Libraries = AutoSyncUtils.GetLibraries(fieldInfo),
            SerializeMethod = serializerNames.serialize,
            DeserializeMethod = serializerNames.deserialize,
            ReadOnly = fieldInfo.IsInitOnly,
            ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            Debug = debug
        };
    }
}
