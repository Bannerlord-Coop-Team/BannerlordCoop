using GameInterface.AutoSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameInterface.AutoSync.Builders
{
    public class AutoSyncBuilderBase
    {
        private readonly AutoSyncRegistry autoSyncRegistry;
        private readonly AutoSyncConstantsBuilder autoSyncConstantsBuilder;

        public AutoSyncBuilderBase(AutoSyncRegistry autoSyncRegistry, AutoSyncConstantsBuilder autoSyncConstantsBuilder)
        {
            this.autoSyncRegistry = autoSyncRegistry;
            this.autoSyncConstantsBuilder = autoSyncConstantsBuilder;
        }

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
                MemberType = AutoSyncUtils.GetMemberTypeName(fieldInfo.FieldType),
                ReadOnly = fieldInfo.IsInitOnly,
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
