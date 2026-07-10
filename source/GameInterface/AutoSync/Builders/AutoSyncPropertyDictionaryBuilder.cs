using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncPropertyDictionaryBuilder : AutoSyncDictionaryBuilderBase
{
    public AutoSyncPropertyDictionaryBuilder(
        IAutoRegistryFactory autoRegistryFactory,
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder, autoRegistryFactory)
    {
    }

    public string GetPrefix(Debuggable<PropertyInfo> propertyItem) => AutoSyncUtils.GetPrefix(propertyItem);

    public IEnumerable<string> GetTranspilers(Debuggable<PropertyInfo> propertyItem)
    {
        yield return TemplateParser.Parse("Patches.PropertyDictionaryChangeTranspilerTemplate", GetTemplateData(propertyItem));
    }

    public IEnumerable<string> GetMessages(Debuggable<PropertyInfo> propertyItem)
    {
        return GetMessagesCore(propertyItem.Value, GetTemplateData(propertyItem));
    }

    public IEnumerable<string> GetSubscriptions(Debuggable<PropertyInfo> propertyItem)
    {
        return GetSubscriptionsCore(GetTemplateData(propertyItem));
    }

    private DictionaryTemplateData GetTemplateData(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        // AutoSyncRegistry.AddProperty rejects setterless properties, so the member is always writable
        return BuildTemplateData(propertyInfo, propertyInfo.PropertyType, propertyItem.Debug);
    }
}
