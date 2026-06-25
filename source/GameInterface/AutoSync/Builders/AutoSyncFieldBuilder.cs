using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldBuilder : AutoSyncBuilderBase
{
    public AutoSyncFieldBuilder(
        IAutoRegistryFactory autoRegistryFactory,
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder, autoRegistryFactory)
    {
    }
    public string GetTranspiler(Debuggable<FieldInfo> fieldInfo)
    {
        return GetSetTranspiler(fieldInfo);
    }

    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;
        var memberIdentifier = AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name);

        var templateData = GetTemplateData(fieldInfo, fieldItem.Debug);
        string localMessage = AutoSyncUtils.GetLocalSetMessage(fieldInfo);
        string networkMessage;
        if (SyncByValue(fieldInfo.FieldType))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
        }

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{memberIdentifier}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{memberIdentifier}_SetNetworkMessage.cs", networkMessage);

        yield return localMessage;
        yield return networkMessage;
    }

    public string GetSubscription(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;
        var memberIdentifier = AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name);

        var templateData = GetTemplateData(fieldInfo, fieldItem.Debug);
        if (SyncByValue(fieldInfo.FieldType))
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
            MemberIdentifier = AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
            MemberType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.FieldType),
            Libraries = AutoSyncUtils.GetLibraries(fieldInfo),
            SerializeMethod = serializerNames.serialize,
            DeserializeMethod = serializerNames.deserialize,
            ReadOnly = fieldInfo.IsInitOnly,
            DirectAccess = fieldInfo.Name == AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
            DirectAssignment = fieldInfo.IsInitOnly == false && fieldInfo.Name == AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
            ReflectionAssignment = fieldInfo.IsInitOnly || fieldInfo.Name != AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
            ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            Debug = debug
        };
    }
}
