using GameInterface.AutoSync.Templates;
using GameInterface.Registry.Auto;
using ProtoBuf.Meta;
using System;
using System.Reflection;

namespace GameInterface.AutoSync.Builders
{
    public class AutoSyncBuilderBase
    {
        private readonly AutoSyncRegistry autoSyncRegistry;
        private readonly AutoSyncConstantsBuilder autoSyncConstantsBuilder;
        private readonly IAutoRegistryFactory autoRegistryFactory;

        public AutoSyncBuilderBase(AutoSyncRegistry autoSyncRegistry, AutoSyncConstantsBuilder autoSyncConstantsBuilder, IAutoRegistryFactory autoRegistryFactory)
        {
            this.autoSyncRegistry = autoSyncRegistry;
            this.autoSyncConstantsBuilder = autoSyncConstantsBuilder;
            this.autoRegistryFactory = autoRegistryFactory;
        }

        // Reference-sync anything the ObjectManager tracks by id (it must resolve by id on the receiver), and
        // only value-sync a genuine non-managed serializable type. CanSerialize alone wrongly value-syncs a
        // registered-but-serializable type (e.g. ItemRoster), deserializing an unregistered copy on the client.
        protected bool SyncByValue(Type type) => RuntimeTypeModel.Default.CanSerialize(type) && !autoRegistryFactory.IsManaged(type);

        protected (string serialize,string deserialize) GetSerializerMethodNames(Type type)
        {
            var serializer = autoSyncRegistry.DefaultSerializer;
            if (autoSyncRegistry.Serializers.ContainsKey(type))
            {
                serializer = autoSyncRegistry.Serializers[type];
            }
            return ($"{serializer.Serialize.DeclaringType.Namespace}.{serializer.Serialize.DeclaringType.Name}.{serializer.Serialize.Name}",
                $"{serializer.Deserialize.DeclaringType.Namespace}.{serializer.Deserialize.DeclaringType.Name}.{serializer.Deserialize.Name}<{type.Name}>");
        }

        protected string GetSetTranspiler(Debuggable<FieldInfo> fieldItem)
        {
            var fieldInfo = fieldItem.Value;

            return TemplateParser.Parse("Patches.FieldSetTranspilerTemplate",
            new
            {
                MemberDeclaringType = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType),
                MemberDeclaringTypeName = AutoSyncUtils.GetSimpleTypeName(fieldInfo.DeclaringType).Replace(".", "_"),
                MemberName = fieldInfo.Name,
                MemberIdentifier = AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
                MemberType = AutoSyncUtils.GetMemberTypeName(fieldInfo.FieldType),
                ReadOnly = fieldInfo.IsInitOnly,
                DirectAccess = fieldInfo.Name == AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
                DirectAssignment = fieldInfo.IsInitOnly == false && fieldInfo.Name == AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
                ReflectionAssignment = fieldInfo.IsInitOnly || fieldInfo.Name != AutoSyncUtils.GetMemberIdentifier(fieldInfo.Name),
                ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
                Debug = fieldItem.Debug
            });
        }

        protected int GetReadOnlyFieldSetter(FieldInfo info)
        {
            return autoSyncConstantsBuilder.AddReadonlyField(info);
        }
    }
}
