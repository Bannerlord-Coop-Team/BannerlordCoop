using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncPropertyBuilder : DynamicSyncBuilderBase
{
    public DynamicSyncPropertyBuilder(
        IObjectManager objectManager,
        DynamicSyncRegistry dynamicSyncRegistry,
        DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
    {
    }
    public string GetPrefix(Debuggable<PropertyInfo> propertyItem) => DynamicSyncUtils.GetPrefix(propertyItem);

    public IEnumerable<string> GetMessages(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        string localMessage = DynamicSyncUtils.GetLocalSetMessage(propertyInfo);
        string networkMessage;
        if (RuntimeTypeModel.Default.CanSerialize(propertyInfo.PropertyType))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
        }

        DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
        DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

        yield return localMessage;
        yield return networkMessage;
    }

    public string GetSubscription(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        if (RuntimeTypeModel.Default.CanSerialize(propertyInfo.PropertyType))
            return TemplateParser.Parse("Handlers.SubscribeSetValueTemplate", templateData);
        else
            return TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", templateData);
    }

    private object GetTemplateData(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var serializerNames = GetSerializerMethodNames(propertyInfo.PropertyType);
        return new
        {
            MemberDeclaringType = propertyInfo.DeclaringType.Name,
            MemberName = propertyInfo.Name,
            MemberType = propertyInfo.PropertyType.Name,
            Libraries = DynamicSyncUtils.GetLibraries(propertyInfo),
            SerializeMethod = serializerNames.serialize,
            DeserializeMethod = serializerNames.deserialize,
            Interface = propertyInfo.PropertyType.IsInterface,
            Debug = propertyItem.Debug
        };
    }
}
