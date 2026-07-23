using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldArrayBuilder : AutoSyncBuilderBase
{
    public AutoSyncFieldArrayBuilder(
        IAutoRegistryFactory autoRegistryFactory,
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder, autoRegistryFactory)
    {
    }

    public string GetTranspiler(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        string setTemplate = GetSetTranspiler(fieldItem);

        string changeTemplate = TemplateParser.Parse("Patches.FieldArrayChangeTranspilerTemplate", GetTemplateData(fieldInfo, fieldItem.Debug));

        return string.Join(Environment.NewLine, setTemplate, changeTemplate);
    }


    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldInfo, fieldItem.Debug);
        string localMessage = AutoSyncUtils.GetLocalSetMessage(fieldInfo);

        string localChangeMessage = TemplateParser.Parse("Messages.LocalArrayChangeMessageTemplate", templateData);

        string networkMessage;
        string networkChangeMessage;
        if (SyncByValue(fieldInfo.FieldType.GetElementType()))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkArraySetValueMessageTemplate", templateData);
            networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkArraySetReferenceMessageTemplate", templateData);
            networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeReferenceMessageTemplate", templateData);
        }

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_ArrayChangeLocalMessage.cs", localChangeMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_ArrayChangeNetworkMessage.cs", networkChangeMessage);

        yield return localMessage;
        yield return localChangeMessage;
        yield return networkMessage;
        yield return networkChangeMessage;
    }

    public string GetSubscription(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldInfo, fieldItem.Debug);
        if (SyncByValue(fieldInfo.FieldType.GetElementType()))
            return TemplateParser.Parse("Handlers.SubscribeArrayValueTemplate", templateData);
        else
            return TemplateParser.Parse("Handlers.SubscribeArrayReferenceTemplate", templateData);
    }
    private string GetArrayType(Type type)
    {
        return type.GetElementType().Name + "[]";
    }
    private object GetTemplateData(FieldInfo fieldInfo, bool debug)
    {
        var serializers = GetSerializerMethodNames(fieldInfo.FieldType.GetElementType());
        return new
        {
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType),
            MemberDeclaringTypeName = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType).Replace(".", "_"),
            MemberName = fieldInfo.Name,
            MemberType = GetArrayType(fieldInfo.FieldType),
            ElementType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.FieldType.GetElementType()),
            Libraries = AutoSyncUtils.GetLibraries(fieldInfo),
            SerializeMethod = serializers.serialize,
            DeserializeMethod = serializers.deserialize,
            ReadOnly = fieldInfo.IsInitOnly,
            ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            Debug = debug
        };
    }
}
