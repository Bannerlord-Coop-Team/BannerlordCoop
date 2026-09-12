using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldPropertyOwnerBuilder : AutoSyncBuilderBase
{
    public AutoSyncFieldPropertyOwnerBuilder(
        IAutoRegistryFactory autoRegistryFactory,
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder, autoRegistryFactory)
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

        string localClearMessage = TemplateParser.Parse("Messages.LocalPropertyOwnerClearMessageTemplate", templateData);
        string networkClearMessage = TemplateParser.Parse("Messages.NetworkPropertyOwnerClearMessageTemplate", templateData);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/Local_{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_PropertyOwnerClear.cs", localClearMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/Network_{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_PropertyOwnerClear.cs", networkClearMessage);

        yield return localMessage;
        yield return networkMessage;
        yield return localClearMessage;
        yield return networkClearMessage;
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
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType),
            MemberDeclaringTypeName = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType).Replace(".", "_"),
            MemberName = fieldInfo.Name,
            MemberType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.FieldType),
            ElementType = AutoSyncUtils.GetSimpleTypeName(GetElementType(fieldInfo.FieldType)),
            Libraries = AutoSyncUtils.GetLibraries(fieldInfo),
            ReadOnly = fieldInfo.IsInitOnly,
            Debug = fieldItem.Debug
        };
    }
}