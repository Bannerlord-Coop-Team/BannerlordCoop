using GameInterface.AutoSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncPropertyBuilder : AutoSyncBuilderBase
{
    public AutoSyncPropertyBuilder(
        IObjectManager objectManager,
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder)
    {
    }
    public string GetPrefix(Debuggable<PropertyInfo> propertyItem) => AutoSyncUtils.GetPrefix(propertyItem);

    public IEnumerable<string> GetMessages(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        string localMessage = AutoSyncUtils.GetLocalSetMessage(propertyInfo);
        string networkMessage;
        var type = propertyInfo.PropertyType;
        if (RuntimeTypeModel.Default.CanSerialize(type))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
        }

        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

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
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.DeclaringType),
            MemberDeclaringTypeName = propertyInfo.DeclaringType.Name,
            MemberName = propertyInfo.Name,
            MemberType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.PropertyType),
            Libraries = AutoSyncUtils.GetLibraries(propertyInfo),
            SerializeMethod = serializerNames.serialize,
            DeserializeMethod = serializerNames.deserialize,
            Interface = propertyInfo.PropertyType.IsInterface,
            Debug = propertyItem.Debug
        };
    }
}
