using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldDictionaryBuilder : AutoSyncDictionaryBuilderBase
{
    public AutoSyncFieldDictionaryBuilder(
        IAutoRegistryFactory autoRegistryFactory,
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder, autoRegistryFactory)
    {
    }

    public IEnumerable<string> GetTranspilers(Debuggable<FieldInfo> fieldItem)
    {
        string setTemplate = GetSetTranspiler(fieldItem);

        string changeTemplate = TemplateParser.Parse("Patches.FieldDictionaryChangeTranspilerTemplate", GetTemplateData(fieldItem));

        yield return string.Join(Environment.NewLine, setTemplate, changeTemplate);
    }

    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        return GetMessagesCore(fieldItem.Value, GetTemplateData(fieldItem));
    }

    public IEnumerable<string> GetSubscriptions(Debuggable<FieldInfo> fieldItem)
    {
        return GetSubscriptionsCore(GetTemplateData(fieldItem));
    }

    private DictionaryTemplateData GetTemplateData(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = BuildTemplateData(fieldInfo, fieldInfo.FieldType, fieldItem.Debug);
        templateData.ReadOnly = fieldInfo.IsInitOnly;
        templateData.ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null;
        return templateData;
    }
}
