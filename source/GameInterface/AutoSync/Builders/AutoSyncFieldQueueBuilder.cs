using GameInterface.AutoSync.Templates;
using GameInterface.Services.ObjectManager;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncFieldQueueBuilder : AutoSyncBuilderBase
{
    public AutoSyncFieldQueueBuilder(
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder) : base(autoSyncRegistry, autoSyncConstantsBuilder)
    {
    }

    public IEnumerable<string> GetTranspilers(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        string setTemplate = GetSetTranspiler(fieldItem);

        string changeTemplate = TemplateParser.Parse("Patches.FieldQueueChangeTranspilerTemplate", GetTemplateData(fieldItem));

        yield return string.Join(Environment.NewLine, setTemplate, changeTemplate);
        yield return TemplateParser.Parse("Patches.QueueClearTranspilerTemplate", GetTemplateData(fieldItem));
    }


    public IEnumerable<string> GetMessages(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldItem);
        string localMessage = AutoSyncUtils.GetLocalSetMessage(fieldInfo);

        string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate", templateData);
        string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);
        string localClearMessage = TemplateParser.Parse("Messages.LocalQueueClearMessageTemplate", templateData);
        string networkClearMessage = TemplateParser.Parse("Messages.NetworkQueueClearMessageTemplate", templateData);

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

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddLocalMessage.cs", localAddMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/Local_{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_QueueClear.cs", localClearMessage);
        AutoSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/Network_{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_QueueClear.cs", networkClearMessage);

        yield return localMessage;
        yield return localAddMessage;
        yield return localRemoveMessage;
        yield return localClearMessage;
        yield return networkMessage;
        yield return networkAddMessage;
        yield return networkRemoveMessage;
        yield return networkClearMessage;
    }

    public IEnumerable<string> GetSubscriptions(Debuggable<FieldInfo> fieldItem)
    {
        var fieldInfo = fieldItem.Value;

        var templateData = GetTemplateData(fieldItem);
        if (RuntimeTypeModel.Default.CanSerialize(GetElementType(fieldInfo.FieldType)))
        {
            yield return TemplateParser.Parse("Handlers.SubscribeQueueValueTemplate", templateData);
        }
        else
        {
            yield return TemplateParser.Parse("Handlers.SubscribeQueueReferenceTemplate", templateData);
        }

        yield return TemplateParser.Parse("Handlers.SubscribeQueueClearTemplate", templateData);
    }

    private string GetQueueTypeNames(Type type)
    {
        return $"Queue<{type.GetGenericArguments()[0].Name}>";
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
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType),
            MemberDeclaringTypeName = fieldInfo.DeclaringType.Name,
            MemberName = fieldInfo.Name,
            MemberType = GetQueueTypeNames(fieldInfo.FieldType),
            ElementType = GetElementType(fieldInfo.FieldType).Name,
            Libraries = AutoSyncUtils.GetLibraries(fieldInfo),
            SerializeMethod = serializers.serialize,
            DeserializeMethod = serializers.deserialize,
            ReadOnly = fieldInfo.IsInitOnly,
            ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            Debug = fieldItem.Debug,
        };
    }
}
