using System;
using System.Collections.Generic;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    /// <summary>
    ///     Adapter of <see cref="MBObjectManager"/> for <see cref="IObjectManager"/>.
    /// </summary>
    public class ObjectManagerAdapter : IObjectManager
    {
        /// <summary>
        ///     Instance of the adapter.
        /// </summary>
        public static ObjectManagerAdapter Instance => m_Instance.Value;

        /// <inheritdoc cref="IObjectManager.Manages{T}"/>
        public bool Manages<T>()
        {
            if (typeof(T).IsSubclassOf(typeof(MBObjectBase)))
            {
                // Managed by MBObjectManager
                return true;
            }

            if (typeof(T) == typeof(Campaign))
            {
                // Global instance
                return true;
            }
            return false;
        }
        
        /// <inheritdoc cref="IObjectManager.Register{T}"/>
        public void Register<T>([NotNull] IObjectLifetimeObserver observer)
        {
            if (m_Handlers.ContainsKey(observer))
            {
                throw new ArgumentException($"{observer} is already a registered handler!", nameof(observer));
            }

            if (typeof(T).IsSubclassOf(typeof(MBObjectBase)))
            {
                m_Handlers[observer] = new ObjectManagerHandler(typeof(T), observer, MBObjectManager.Instance);
            }
            else if (typeof(T) == typeof(Campaign))
            {
                m_Handlers[observer] = new CampaignLifetimeHandler(observer);
            }
        }

        /// <inheritdoc cref="IObjectManager.Unregister"/>
        public void Unregister(IObjectLifetimeObserver observer)
        {
            if (m_Handlers.TryGetValue(observer, out IHandler handler))
            {
                m_Handlers.Remove(observer);
                handler.Unregister();
            }
        }

        #region Private

        private interface IHandler
        {
            void Unregister();
        }
        
        /// <summary>
        ///     Handler implementation to track object creation & unregister in a <see cref="MBObjectManager"/>.
        ///     The current interface does not allow to track registration for some reason. This is annoying because
        ///     during serialization (load save), the objects are not actually created but loaded in an initialized
        ///     state directly to memory and then internally registered.
        /// </summary>
        private class ObjectManagerHandler : IObjectManagerHandler, IHandler
        {
            public ObjectManagerHandler(
                Type type, 
                [NotNull] IObjectLifetimeObserver observer,
                MBObjectManager mbObjectManager)
            {
                RegisteredType = type;
                m_Observer = observer;
                if (mbObjectManager != null)
                {
                    Register(mbObjectManager);
                }
            }
            
            public void AfterCreateObject(MBObjectBase objectBase)
            {
                if (objectBase.GetType() == RegisteredType)
                {
                    m_Observer.AfterRegisterObject(objectBase);
                }
            }

            public void RegisterObjectWithoutInitialization(MBObjectBase objectBase)
            {
                if (objectBase.GetType() == RegisteredType)
                {
                    m_Observer.AfterRegisterObject(objectBase);
                }
            }

            public void AfterUnregisterObject(MBObjectBase objectBase)
            {
                if (objectBase.GetType() == RegisteredType)
                {
                    m_Observer.AfterUnregisterObject(objectBase);
                }
            }

            public void Register(MBObjectManager manager)
            {
                Unregister();
                m_Manager = manager;
                m_Manager.AddHandler(this);
            }
            
            public void Unregister()
            {
                if (m_Manager != null)
                {
                    m_Manager.RemoveHandler(this);
                    m_Manager = null;
                }
            }

            private MBObjectManager m_Manager;
            private readonly IObjectLifetimeObserver m_Observer;
            public Type RegisteredType { get; }
        }
        /// <summary>
        ///     Called when the <see cref="MBObjectManager"/> instance is initialized. Happens when a game is loaded.
        /// </summary>
        public void OnMBObjectManagerInit()
        {
            MBObjectManager manager = MBObjectManager.Instance;
            if (manager == null)
            {
                return;
            }
            
            foreach (var pair in m_Handlers)
            {
                if (pair.Value is ObjectManagerHandler handler)
                {
                    manager.AddHandler(handler);
                }
            }
        }
        /// <summary>
        ///     Postfix for the Init method of <see cref="MBObjectManager"/>.
        /// </summary>
        [HarmonyPatch(typeof(MBObjectManager), "Init", new Type[]{})]
        private static class MBObjectManagerPatch
        {
            static void Postfix()
            {
                Instance.OnMBObjectManagerInit();
            }
        }
        
        /// <summary>
        ///     Called when an existing game object was registered with the <see cref="MBObjectManager"/>. As of
        ///     writing of this comment, this only happens in the context of save game serialization where game
        ///     objects are NOT created using <see cref="IObjectManagerHandler"/> AfterCreateObject, so this method
        ///     is called instead.
        /// </summary>
        /// <param name="obj"></param>
        private void OnAfterRegisterObjectWithoutInitialization(MBObjectBase obj)
        {
            foreach (KeyValuePair<IObjectLifetimeObserver,IHandler> pair in m_Handlers)
            {
                if (pair.Value is ObjectManagerHandler handler)
                {
                    handler.RegisterObjectWithoutInitialization(obj);
                }
            }
        }

        /// <summary>
        ///     Postfix for the registration method of <see cref="MBObjectManager"/> that handles already initialized
        ///     objects. This is used during serialization (save game load).
        /// </summary>
        [HarmonyPatch(typeof(MBObjectManager), "TryRegisterObjectWithoutInitialization", new Type[]{typeof(MBObjectBase)})]
        private static class TryRegisterObjectWithoutInitializationPatch
        {
            static void Postfix(MBObjectBase obj)
            {
                Instance.OnAfterRegisterObjectWithoutInitialization(obj);
            }
        }

        /// <summary>
        ///     Kind of a workaround. Taleworlds change initalization logic in 1.6.0 after loading a file.
        ///     Some classes explicitly skip calling MBObjectBase.OnBeforeLoad. Not sure why and i can imagine
        ///     that this will be changed again. Looks quite hacky in the disassembly.
        ///     
        ///     Affected:
        ///     - MobileParty
        ///     - Clan
        ///     - Hero
        ///     - Kingdom
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "OnBeforeLoad")]
        private static class MobileParty_OnBeforeLoad
        {
            static void Postfix(MobileParty __instance)
            {
                Instance.OnAfterRegisterObjectWithoutInitialization(__instance);
            }
        }
        [HarmonyPatch(typeof(Clan), "OnBeforeLoad")]
        private static class Clan_OnBeforeLoad
        {
            static void Postfix(Clan __instance)
            {
                Instance.OnAfterRegisterObjectWithoutInitialization(__instance);
            }
        }
        //[HarmonyPatch(typeof(Hero), "OnBeforeLoad")] Not used, replacement?
        //private static class Hero_OnBeforeLoad
        //{
        //    static void Postfix(Hero __instance)
        //    {
        //        Instance.OnAfterRegisterObjectWithoutInitialization(__instance);
        //    }
        //}
        [HarmonyPatch(typeof(Kingdom), "OnBeforeLoad")]
        private static class Kingdom_OnBeforeLoad
        {
            static void Postfix(Kingdom __instance)
            {
                Instance.OnAfterRegisterObjectWithoutInitialization(__instance);
            }
        }

        private class CampaignLifetimeHandler : IHandler
        {
            public CampaignLifetimeHandler([NotNull] IObjectLifetimeObserver observer)
            {
                m_Observer = observer;
                Main.Instance.OnGameInit += OnGameInit;
            }

            private void OnGameInit(Game game)
            {
                if (game.GameType is Campaign campaign)
                {
                    m_Observer.AfterRegisterObject(campaign);
                }
            }
            public void Unregister()
            {
                Main.Instance.OnGameInit -= OnGameInit;
            }

            private readonly IObjectLifetimeObserver m_Observer;
        }
        
        private static readonly Lazy<ObjectManagerAdapter> m_Instance =
            new Lazy<ObjectManagerAdapter>(() => new ObjectManagerAdapter());

        private Dictionary<IObjectLifetimeObserver, IHandler> m_Handlers = new Dictionary<IObjectLifetimeObserver, IHandler>();

        #endregion
    }
}