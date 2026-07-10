using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

/// <summary>
/// Shared generation logic for <see cref="AutoSyncFieldDictionaryBuilder"/> and
/// <see cref="AutoSyncPropertyDictionaryBuilder"/>. Unlike the single-element collections, a
/// dictionary carries a key AND a value, each independently synced by value (protobuf) or by
/// reference (object manager id), so the templates are driven by one data object instead of
/// separate value/reference template files.
/// </summary>
public class AutoSyncDictionaryBuilderBase : AutoSyncBuilderBase
{
    public AutoSyncDictionaryBuilderBase(
        AutoSyncRegistry autoSyncRegistry,
        AutoSyncConstantsBuilder autoSyncConstantsBuilder,
        IAutoRegistryFactory autoRegistryFactory) : base(autoSyncRegistry, autoSyncConstantsBuilder, autoRegistryFactory)
    {
    }

    protected class DictionaryTemplateData
    {
        public string MemberDeclaringType { get; set; }
        public string MemberDeclaringTypeName { get; set; }
        public string MemberName { get; set; }
        public string MemberType { get; set; }
        public string KeyType { get; set; }
        public string ValueType { get; set; }
        public bool KeyByValue { get; set; }
        public bool ValueByValue { get; set; }
        public string KeyWireType { get; set; }
        public string ValueWireType { get; set; }
        public string KeySerializeMethod { get; set; }
        public string KeyDeserializeMethod { get; set; }
        public string ValueSerializeMethod { get; set; }
        public string ValueDeserializeMethod { get; set; }
        public IEnumerable<string> Libraries { get; set; }
        public bool ReadOnly { get; set; }
        public int? ReadOnlySetterIndex { get; set; }
        public bool Debug { get; set; }

        /// <summary>The remove message reuses Messages.LocalCollectionRemoveMessageTemplate, which keys off ElementType.</summary>
        public string ElementType => KeyType;
    }

    protected DictionaryTemplateData BuildTemplateData(MemberInfo memberInfo, Type memberType, bool debug)
    {
        var genericArguments = memberType.GetGenericArguments();
        var keyType = genericArguments[0];
        var valueType = genericArguments[1];

        ValidateSyncable(memberInfo, keyType, "key");
        ValidateSyncable(memberInfo, valueType, "value");

        bool keyByValue = SyncByValue(keyType);
        bool valueByValue = SyncByValue(valueType);

        var keySerializers = GetSerializerMethodNames(keyType);
        var valueSerializers = GetSerializerMethodNames(valueType);

        var libraries = new List<string>
        {
            memberInfo.DeclaringType.Namespace,
            AutoSyncUtils.GetNamespace(keyType),
            AutoSyncUtils.GetNamespace(valueType),
        };

        return new DictionaryTemplateData
        {
            MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(memberInfo.DeclaringType),
            MemberDeclaringTypeName = AutoSyncUtils.GetSimpleTypeName(memberInfo.DeclaringType).Replace(".", "_"),
            MemberName = memberInfo.Name,
            MemberType = AutoSyncUtils.GetMemberTypeName(memberType),
            KeyType = AutoSyncUtils.GetSimpleTypeName(keyType),
            ValueType = AutoSyncUtils.GetSimpleTypeName(valueType),
            KeyByValue = keyByValue,
            ValueByValue = valueByValue,
            KeyWireType = keyByValue ? "byte[]" : "string",
            ValueWireType = valueByValue ? "byte[]" : "string",
            KeySerializeMethod = keySerializers.serialize,
            KeyDeserializeMethod = keySerializers.deserialize,
            ValueSerializeMethod = valueSerializers.serialize,
            ValueDeserializeMethod = valueSerializers.deserialize,
            Libraries = libraries.Where(l => l != null).Distinct().ToList(),
            Debug = debug,
        };
    }

    private void ValidateSyncable(MemberInfo memberInfo, Type type, string role)
    {
        // By value needs protobuf to serialize it; by reference needs the object manager to resolve
        // it by id on the receiver. A type that can do neither would generate dead (or for structs,
        // uncompilable) sync code, so fail at build time with an actionable message.
        if (SyncByValue(type)) return;
        if (IsManaged(type) && type.IsValueType == false) return;

        throw new NotSupportedException(
            $"{memberInfo.DeclaringType?.Name}.{memberInfo.Name}: dictionary {role} type {type.Name} is neither " +
            $"protobuf serializable (add a surrogate to SurrogateCollection) nor managed by a registry, so it cannot be auto synced");
    }

    protected IEnumerable<string> GetMessagesCore(MemberInfo memberInfo, DictionaryTemplateData templateData)
    {
        string localSetMessage = AutoSyncUtils.GetLocalSetMessage(memberInfo);
        string networkSetMessage = TemplateParser.Parse("Messages.NetworkDictionarySetMessageTemplate", templateData);

        // Add and indexer set share one upsert message pair - their receiving-side apply is identical
        string localUpsertMessage = TemplateParser.Parse("Messages.LocalDictionaryUpsertMessageTemplate", templateData);
        string networkUpsertMessage = TemplateParser.Parse("Messages.NetworkDictionaryUpsertMessageTemplate", templateData);

        string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate", templateData);
        string networkRemoveMessage = TemplateParser.Parse("Messages.NetworkDictionaryRemoveMessageTemplate", templateData);

        string localClearMessage = TemplateParser.Parse("Messages.LocalDictionaryClearMessageTemplate", templateData);
        string networkClearMessage = TemplateParser.Parse("Messages.NetworkDictionaryClearMessageTemplate", templateData);

        var declaringTypeName = memberInfo.DeclaringType.Name;
        var memberName = memberInfo.Name;

        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/{declaringTypeName}_{memberName}_SetLocalMessage.cs", localSetMessage);
        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/{declaringTypeName}_{memberName}_SetNetworkMessage.cs", networkSetMessage);

        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/{declaringTypeName}_{memberName}_UpsertLocalMessage.cs", localUpsertMessage);
        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/{declaringTypeName}_{memberName}_UpsertNetworkMessage.cs", networkUpsertMessage);

        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/{declaringTypeName}_{memberName}_RemoveLocalMessage.cs", localRemoveMessage);
        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/{declaringTypeName}_{memberName}_RemoveNetworkMessage.cs", networkRemoveMessage);

        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/Local_{declaringTypeName}_{memberName}_DictionaryClear.cs", localClearMessage);
        AutoSyncConfiguration.ExportFile($"{declaringTypeName}/Network_{declaringTypeName}_{memberName}_DictionaryClear.cs", networkClearMessage);

        yield return localSetMessage;
        yield return localUpsertMessage;
        yield return localRemoveMessage;
        yield return localClearMessage;
        yield return networkSetMessage;
        yield return networkUpsertMessage;
        yield return networkRemoveMessage;
        yield return networkClearMessage;
    }

    protected IEnumerable<string> GetSubscriptionsCore(DictionaryTemplateData templateData)
    {
        yield return TemplateParser.Parse("Handlers.SubscribeDictionaryTemplate", templateData);
        yield return TemplateParser.Parse("Handlers.SubscribeDictionaryClearTemplate", templateData);
    }
}
