using Common.Messaging;
using Common.Serialization;
using GameInterface.DynamicSync.Builders;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncPatcher
    {
        public Assembly Assembly;

        private readonly Harmony harmony;
        private readonly DynamicSyncBuilder dynamicSyncBuilder;
        private readonly DynamicHandler dynamicHandler;
        private readonly ISerializableTypeMapper serializableTypeMapper;

        public DynamicSyncPatcher(Harmony harmony, DynamicSyncBuilder dynamicSyncBuilder, DynamicHandler dynamicHandler, ISerializableTypeMapper serializableTypeMapper)
        {
            this.harmony = harmony;
            this.dynamicSyncBuilder = dynamicSyncBuilder;
            this.dynamicHandler = dynamicHandler;
            this.serializableTypeMapper = serializableTypeMapper;
        }

        /// <summary>
        /// Only required for testing to be able to rebind the handlers on client side
        /// </summary>
        /// <param name="assembly"></param>
        public void BindHandlers(Assembly assembly)
        {
            serializableTypeMapper.AddTypes(DynamicSyncRegistry.Assembly.GetTypes()
            .Where(type => {
                try
                {
                    return type.IsDefined(typeof(ProtoContractAttribute), inherit: false);
                }
                // Some types have malformed attributes?
                catch (CustomAttributeFormatException)
                {
                    return false;
                }
            }));

            foreach (var handler in DynamicSyncRegistry.DynamicHandlers)
            {
                dynamicHandler.RegisterHandler(handler);
            }
        }

        public void PatchAll()
        {
            if (!DynamicSyncConfiguration.Enabled)
                return;

            Assembly = dynamicSyncBuilder.Build();
            harmony.PatchAllUncategorized();

            BindHandlers(Assembly);
        }
    }
}
