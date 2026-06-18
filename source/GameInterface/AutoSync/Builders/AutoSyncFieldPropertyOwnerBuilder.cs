using GameInterface.AutoSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldPropertyOwnerBuilder : AutoSyncBuilderBase
{
    public AutoSyncFieldPropertyOwnerBuilder(
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder)
    {
    }

    public string GetTranspiler(Debuggable<FieldInfo> fieldItem)
    {
        return TemplateParser.Parse("Patches.FieldPropertyOwnerChangeTranspilerTemplate", GetTemplateData(fieldItem));
    }

    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;
        var templateData = GetTemplateData(fieldItem);

        string localMessage = TemplateParser.Parse("Messages.LocalPropertyOwnerSetMessageTemplate", templateData);
        string networkMessage = TemplateParser.Parse("Messages.NetworkPropertyOwnerSetReferenceMessageTemplate", templateData);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

        yield return localMessage;
        yield return networkMessage;
    }

    public string GetSubscription(Debuggable<FieldInfo> fieldItem)
    {
        return TemplateParser.Parse("Handlers.SubscribePropertyOwnerReferenceTemplate", GetTemplateData(fieldItem));
    }

    private Type GetElementType(Type type)
    {
        return type.GetGenericArguments()[0];
    }

    private object GetTemplateData(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;
        return new
        {
            MemberDeclaringType = fieldInfo.DeclaringType.Name,
            MemberName = fieldInfo.Name,
            MemberType = fieldInfo.FieldType.Name,
            ElementType = GetElementType(fieldInfo.FieldType).Name,
            Libraries = AutoSyncUtils.GetLibraries(fieldInfo),
            ReadOnly = fieldInfo.IsInitOnly,
            Debug = fieldItem.Debug
        };
    }
}