using Common.Messaging;
using Common.Serialization;
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
        private readonly Harmony harmony;
        private readonly DynamicSyncRegistry dynamicSyncRegistry;
        private readonly IObjectManager objectManager;
        private readonly DynamicHandler dynamicHandler;
        private readonly ISerializableTypeMapper serializableTypeMapper;

        public DynamicSyncPatcher(Harmony harmony, DynamicSyncRegistry dynamicSyncRegistry, IObjectManager objectManager, DynamicHandler dynamicHandler, ISerializableTypeMapper serializableTypeMapper)
        {
            this.harmony = harmony;
            this.dynamicSyncRegistry = dynamicSyncRegistry;
            this.objectManager = objectManager;
            this.dynamicHandler = dynamicHandler;
            this.serializableTypeMapper = serializableTypeMapper;
        }

        public Assembly BuildAssembly()
        {
            dynamicSyncRegistry.Build();

            BindHandlers();
            
            return DynamicSyncRegistry.Assembly;
        }

        public void BindHandlers()
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
    }
}
