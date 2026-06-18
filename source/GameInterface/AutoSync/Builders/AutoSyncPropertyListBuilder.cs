using GameInterface.AutoSync.Templates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncPropertyListBuilder : AutoSyncBuilderBase
{
    public AutoSyncPropertyListBuilder(
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder)
    {
    }
    public string GetPrefix(Debuggable<PropertyInfo> propertyItem) => AutoSyncUtils.GetPrefix(propertyItem);


    public string GetTranspiler(Debuggable<PropertyInfo> propertyItem)
    {
        string changeTemplate = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", GetTemplateData(propertyItem));

        return changeTemplate;
    }


    public IEnumerable<string> GetMessages(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        string localMessage = AutoSyncUtils.GetLocalSetMessage(propertyInfo);

        string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate", templateData);
        string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);

        string networkMessage;
        string networkAddMessage;
        string networkRemoveMessage;
        if (RuntimeTypeModel.Default.CanSerialize(GetElementType(propertyInfo.PropertyType)))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetValueMessageTemplate", templateData);
            networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddValueMessageTemplate", templateData);
            networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetReferenceMessageTemplate", templateData);
            networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddReferenceMessageTemplate", templateData);
            networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveReferenceMessageTemplate", templateData);
        }

        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddLocalMessage.cs", localAddMessage);
        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

        yield return localMessage;
        yield return localAddMessage;
        yield return localRemoveMessage;
        yield return networkMessage;
        yield return networkAddMessage;
        yield return networkRemoveMessage;
    }

    public string GetSubscription(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        if (RuntimeTypeModel.Default.CanSerialize(GetElementType(propertyInfo.PropertyType)))
        {
            return TemplateParser.Parse("Handlers.SubscribeCollectionValueTemplate", templateData);
        }
        else
        {
            return TemplateParser.Parse("Handlers.SubscribeCollectionReferenceTemplate", templateData);
        }
    }
    private string GetListTypeName(Type type)
    {
        return $"List<{type.GetGenericArguments()[0].Name}>";
    }

    private Type GetElementType(Type type)
    {
        return type.GetGenericArguments()[0];
    }

    private object GetTemplateData(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var serializers = GetSerializerMethodNames(GetElementType(propertyInfo.PropertyType));
        return new
        {
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.DeclaringType),
            MemberDeclaringTypeName = propertyInfo.DeclaringType.Name,
            MemberName = propertyInfo.Name,
            MemberType = GetListTypeName(propertyInfo.PropertyType),
            ElementType = AutoSyncUtils.GetSimpleTypeName(GetElementType(propertyInfo.PropertyType)),
            Libraries = AutoSyncUtils.GetLibraries(propertyInfo),
            NotReadOnly = propertyInfo.SetMethod != null,
            SerializeMethod = serializers.serialize,
            DeserializeMethod = serializers.deserialize,
            Debug = propertyItem.Debug,
        };
    }
}
