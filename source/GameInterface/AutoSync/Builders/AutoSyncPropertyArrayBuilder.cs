using GameInterface.AutoSync.Templates;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncPropertyArrayBuilder : AutoSyncBuilderBase
{
    public AutoSyncPropertyArrayBuilder(
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder)
    {
    }
    public string GetPrefix(Debuggable<PropertyInfo> propertyInfo) => AutoSyncUtils.GetPrefix(propertyInfo);

    public string GetTranspiler(Debuggable<PropertyInfo> propertyitem)
    {
        var propertyInfo = propertyitem.Value;

        return TemplateParser.Parse("Patches.PropertyArrayChangeTranspilerTemplate",
                new
                {
                    MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.DeclaringType),
                    MemberDeclaringTypeName = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = GetArrayType(propertyInfo.PropertyType),
                    ElementType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.PropertyType.GetElementType()),
                    Libraries = AutoSyncUtils.GetLibraries(propertyInfo),
                    Debug = propertyitem.Debug
                });
    }


    public IEnumerable<string> GetMessages(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        string localMessage = AutoSyncUtils.GetLocalSetMessage(propertyInfo);

        string localChangeMessage = TemplateParser.Parse("Messages.LocalArrayChangeMessageTemplate", templateData);

        string networkMessage;
        string networkChangeMessage;
        if(RuntimeTypeModel.Default.CanSerialize(propertyInfo.PropertyType.GetElementType()))
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkArraySetValueMessageTemplate", templateData);
            networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeValueMessageTemplate", templateData);
        }
        else
        {
            networkMessage = TemplateParser.Parse("Messages.NetworkArraySetReferenceMessageTemplate", templateData);
            networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeReferenceMessageTemplate", templateData);
        }

        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_ArrayChangeLocalMessage.cs", localChangeMessage);
        AutoSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_ArrayChangeNetworkMessage.cs", networkChangeMessage);

        yield return localMessage;
        yield return localChangeMessage;
        yield return networkMessage;
        yield return networkChangeMessage;
    }

    public string GetSubscription(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var templateData = GetTemplateData(propertyItem);
        if (RuntimeTypeModel.Default.CanSerialize(propertyInfo.PropertyType.GetElementType()))
        {
            return TemplateParser.Parse("Handlers.SubscribeArrayValueTemplate", templateData);
        }
        else
        {
            return TemplateParser.Parse("Handlers.SubscribeArrayReferenceTemplate", templateData);
        }
    }
    private string GetArrayType(Type type)
    {
        return type.GetElementType().Name + "[]";
    }

    private object GetTemplateData(Debuggable<PropertyInfo> propertyItem)
    {
        var propertyInfo = propertyItem.Value;

        var serializers = GetSerializerMethodNames(propertyInfo.PropertyType.GetElementType());
        return new
        {
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.DeclaringType),
            MemberDeclaringTypeName = propertyInfo.DeclaringType.Name,
            MemberName = propertyInfo.Name,
            MemberType = GetArrayType(propertyInfo.PropertyType),
            ElementType = AutoSyncUtils.GetSimpleTypeName(propertyInfo.PropertyType.GetElementType()),
            Libraries = AutoSyncUtils.GetLibraries(propertyInfo),
            NotReadOnly = propertyInfo.SetMethod != null,
            SerializeMethod = serializers.serialize,
            DeserializeMethod = serializers.deserialize,
            Debug = propertyItem.Debug
        };
    }
}
