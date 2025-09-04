using System;
using System.Collections.Generic;
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
    }
}
