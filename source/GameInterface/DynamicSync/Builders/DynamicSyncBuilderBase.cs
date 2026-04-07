using GameInterface.DynamicSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncBuilderBase
    {
        private readonly DynamicSyncRegistry dynamicSyncRegistry;
        private readonly DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder;

        public DynamicSyncBuilderBase(DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder)
        {
            this.dynamicSyncRegistry = dynamicSyncRegistry;
            this.dynamicSyncConstantsBuilder = dynamicSyncConstantsBuilder;
        }

        protected (string serialize,string deserialize) GetSerializerMethodNames(Type type)
        {
            var serializer = dynamicSyncRegistry.DefaultSerializer;
            if (dynamicSyncRegistry.Serializers.ContainsKey(type))
            {
                serializer = dynamicSyncRegistry.Serializers[type];
            }
            return ($"{serializer.Serialize.DeclaringType.Namespace}.{serializer.Serialize.DeclaringType.Name}.{serializer.Serialize.Name}",
                $"{serializer.Deserialize.DeclaringType.Namespace}.{serializer.Deserialize.DeclaringType.Name}.{serializer.Deserialize.Name}<{type.Name}>");
        }

        protected string GetSetTranspiler(FieldInfo fieldInfo)
        {
            return TemplateParser.Parse("Patches.FieldSetTranspilerTemplate",
            new
            {
                MemberDeclaringType = fieldInfo.DeclaringType.Name,
                MemberName = fieldInfo.Name,
                MemberType = DynamicSyncUtils.GetMemberTypeName(fieldInfo.FieldType),
                ReadOnly = fieldInfo.IsInitOnly,
                ReadOnlySetterIndex = fieldInfo.IsInitOnly ? GetReadOnlyFieldSetter(fieldInfo) : (int?)null,
            });
        }

        protected int GetReadOnlyFieldSetter(FieldInfo info)
        {
            return dynamicSyncConstantsBuilder.AddReadonlyField(info);
        }
    }
}
