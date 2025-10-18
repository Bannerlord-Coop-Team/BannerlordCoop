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

        public DynamicSyncBuilderBase(DynamicSyncRegistry dynamicSyncRegistry)
        {
            this.dynamicSyncRegistry = dynamicSyncRegistry;
        }

        protected (string serialize,string deserialize) GetSerializerMethodNames(Type type)
        {
            var serializer = dynamicSyncRegistry.DefaultSerializer;
            if (dynamicSyncRegistry.Serializers.ContainsKey(type))
            {
                serializer = dynamicSyncRegistry.Serializers[type];
            }
            return ($"{serializer.Serialize.DeclaringType.Namespace}.{serializer.Serialize.DeclaringType.Name}.{serializer.Serialize.Name}",
                $"{serializer.Deserialize.DeclaringType.Namespace}.{serializer.Deserialize.DeclaringType.Name}.{serializer.Deserialize.Name}");
        }

        protected int GetReadOnlyFieldSetter(FieldInfo info)
        {
            var method = new DynamicMethod(
              name: info.DeclaringType.Name + info.Name + "Setter",
              returnType: null,
              parameterTypes: new[] { info.DeclaringType, info.FieldType },
              restrictedSkipVisibility: true
            );

            var gen = method.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, info);
            gen.Emit(OpCodes.Ret);

            var actionType = typeof(Action<,>).MakeGenericType(info.DeclaringType, info.FieldType);

            // TODO: find a way to store the delegate without needing the DynamicInvoke if performance is an issue
            var del = method.CreateDelegate(actionType);
            var action = (object a, object b) => { del.DynamicInvoke(a, b); };
            return dynamicSyncRegistry.AddReadOnlySetter(action);
        }
    }
}
