using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncFieldListBuilder : DynamicSyncBuilderBase
{
    public DynamicSyncFieldListBuilder(
        DynamicSyncRegistry dynamicSyncRegistry,
        DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder) : base(dynamicSyncRegistry, dynamicSyncConstantsBuilder)
    {
    }

    public string GetTranspiler(Debuggable<FieldInfo> fieldItem)
    {
        string setTemplate = GetSetTranspiler(fieldItem);
        string changeTemplate = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", GetTemplateData(fieldItem));
        return string.Join(Environment.NewLine, setTemplate, changeTemplate);
    }


    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldItem);
        string localMessage = DynamicSyncUtils.GetLocalSetMessage(fieldInfo);

        string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate", templateData);
        string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);

        string networkMessage;
        string networkAddMessage;
        string networkRemoveMessage;
        if (RuntimeTypeModel.Default.CanSerialize(GetElementType(fieldInfo.FieldType)))
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

        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddLocalMessage.cs", localAddMessage);
        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

        yield return localMessage;
        yield return localAddMessage;
        yield return localRemoveMessage;
        yield return networkMessage;
        yield return networkAddMessage;
        yield return networkRemoveMessage;
    }

    public string GetSubscription(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldItem);
        if (RuntimeTypeModel.Default.CanSerialize(GetElementType(fieldInfo.FieldType)))
            return TemplateParser.Parse("Handlers.SubscribeCollectionValueTemplate", templateData);
        else
            return TemplateParser.Parse("Handlers.SubscribeCollectionReferenceTemplate", templateData);
    }
    private string GetListTypeName(Type type)
    {
        return $"List<{type.GetGenericArguments()[0].Name}>";
    }

    private Type GetElementType(Type type)
    {
        return type.GetGenericArguments()[0];
    }

    private object GetTemplateData(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var serializers = GetSerializerMethodNames(GetElementType(fieldInfo.FieldType));
        return new
        {
            MemberDeclaringType = fieldInfo.DeclaringType.Name,
            MemberName = fieldInfo.Name,
            MemberType = GetListTypeName(fieldInfo.FieldType),
            ElementType = GetElementType(fieldInfo.FieldType).Name,
            Libraries = DynamicSyncUtils.GetLibraries(fieldInfo),
            SerializeMethod = serializers.serialize,
            DeserializeMethod = serializers.deserialize,
            ReadOnly = fieldInfo.IsInitOnly,
            ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            Debug = fieldItem.Debug
        };
    }
}
