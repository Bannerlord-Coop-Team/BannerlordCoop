using System;
using System.Collections.Generic;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
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
            return false;
        }
        
        /// <inheritdoc cref="IObjectManager.Register{T}"/>
        public void Register<T>([NotNull] IObjectLifetimeObserver observer)
        {
            if (m_Handlers.ContainsKey(observer))
            {
                throw new ArgumentException($"{observer} is already a registered handler!", nameof(observer));
            }
            Handler handler = new Handler(typeof(T), observer);
            m_Handlers[observer] = handler;

            MBObjectManager manager = MBObjectManager.Instance;
            if (manager != null)
            {
                manager.AddHandler(handler);
            }
        }

        /// <inheritdoc cref="IObjectManager.Unregister"/>
        public void Unregister(IObjectLifetimeObserver observer)
        {
            if (m_Handlers.TryGetValue(observer, out Handler handler))
            {
                m_Handlers.Remove(observer);
                
                MBObjectManager manager = MBObjectManager.Instance;
                if (manager != null)
                {
                    manager.RemoveHandler(handler);
                }
            }
        }

        #region Private
        /// <summary>
        ///     Handler implementation to track object creation & unregister in a <see cref="MBObjectManager"/>.
        ///     The current interface does not allow to track registration for some reason. This is annoying because
        ///     during serialization (load save), the objects are not actually created but loaded in an initialized
        ///     state directly to memory and then internally registered.
        /// </summary>
        private class Handler : IObjectManagerHandler
        {
            public Handler(Type type, [NotNull] IObjectLifetimeObserver observer)
            {
                RegisteredType = type;
                m_Observer = observer;
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
            
            foreach (var handler in m_Handlers)
            {
                manager.AddHandler(handler.Value);
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
            foreach (KeyValuePair<IObjectLifetimeObserver,Handler> pair in m_Handlers)
            {
                pair.Value.RegisterObjectWithoutInitialization(obj);
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
        
        
        private static readonly Lazy<ObjectManagerAdapter> m_Instance =
            new Lazy<ObjectManagerAdapter>(() => new ObjectManagerAdapter());

        private Dictionary<IObjectLifetimeObserver, Handler> m_Handlers = new Dictionary<IObjectLifetimeObserver, Handler>();

        #endregion
    }
}