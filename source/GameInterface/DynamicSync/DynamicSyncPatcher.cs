using Common.Messaging;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
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

        public DynamicSyncPatcher(Harmony harmony, DynamicSyncRegistry dynamicSyncRegistry, IObjectManager objectManager, DynamicHandler dynamicHandler)
        {
            this.harmony = harmony;
            this.dynamicSyncRegistry = dynamicSyncRegistry;
            this.objectManager = objectManager;
            this.dynamicHandler = dynamicHandler;
        }

        public void PatchAll()
        {
            var assembly = dynamicSyncRegistry.Build(objectManager);

            foreach (var handler in GetDynamicHandlerClasses(assembly))
            {
                dynamicHandler.RegisterHandler(handler);
            }

            harmony.PatchCategory(assembly, GameInterface.HARMONY_STATIC_FIXES_CATEGORY);
            harmony.PatchAllUncategorized(assembly);
        }

        private IEnumerable<Type> GetDynamicHandlerClasses(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.GetInterface(nameof(IHandler)) != null &&
                            t.IsClass &&
                            t.IsGenericType == false &&
                            t.IsAbstract == false);
            return types;
        }
    }
}
