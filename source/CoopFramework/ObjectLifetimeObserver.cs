using System;
using System.Linq;
using JetBrains.Annotations;
using NLog;
using Sync.Behaviour;
using Sync.Patch;

namespace CoopFramework
{
    /// <summary>
    ///     Observer for an object lifetimes based on the types constructor and destructors invocations. Note that this
    ///     only works for types that:
    ///     1. are only constructed using any of the constructors. This may not be the case for memory serialized classes
    ///     2. Implement a destructor
    ///
    ///     There is not way to force this, it entirely depends on the object management of the game. Use a game specific
    ///     <see cref="IObjectManager"/> implementation instead.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectLifetimeObserver<T> : IObjectLifetimeObserver where T : class
    {
        /// <summary>
        ///     Invoked after an object was created.
        /// </summary>
        public Action<T> AfterCreateObject;
        
        /// <summary>
        ///     Invoked just before an object is deconstructed.
        /// </summary>
        public Action<T> AfterRemoveObject;

        /// <summary>
        ///     Called after an instance of the object was registered with the object manager. The object may be in
        ///     an uninitialized state, but it is created.
        /// </summary>
        /// <param name="createdObject"></param>
        /// <exception cref="Exception"></exception>
        public void AfterRegisterObject(object createdObject)
        {
            if (!(createdObject is T instance)) throw new Exception("Unexpected object type.");
            AfterCreateObject?.Invoke(instance);
        }

        /// <summary>
        ///     Called after an instance of the object was unregistered with the object manager.
        /// </summary>
        /// <param name="removedObject"></param>
        /// <exception cref="Exception"></exception>
        public void AfterUnregisterObject(object removedObject)
        {
            if (!(removedObject is T instance)) throw new Exception("Unexpected object type.");
            AfterRemoveObject?.Invoke(instance);
        }

        /// <summary>
        ///     Patches all constructors of <typeparamref name="T"/>.
        /// </summary>
        /// <returns>Have any constructors been patched?</returns>
        public bool PatchConstruction()
        {
            m_ConstructorPatch = new ConstructorPatch<ObjectLifetimeObserver<T>>(typeof(T)).PostfixAll();
            if (!m_ConstructorPatch.Methods.Any())
                return false;

            foreach (var methodAccess in m_ConstructorPatch.Methods)
                methodAccess.Postfix.SetGlobalHandler((origin, instance, args) =>
                {
                    AfterRegisterObject(instance as T);
                });
            return true;
        }

        /// <summary>
        ///     Patches all desctructors of <typeparamref name="T"/>
        /// </summary>
        /// <returns>Have any destructors been patched?</returns>
        public bool PatchDeconstruction()
        {
            m_DestructorPatch = new DestructorPatch<ObjectLifetimeObserver<T>>(typeof(T)).Prefix();
            if (!m_DestructorPatch.Methods.Any())
                return false;
            foreach (var methodAccess in m_DestructorPatch.Methods)
                methodAccess.Prefix.SetGlobalHandler((origin, instance, args) =>
                {
                    AfterUnregisterObject(instance);
                    return ECallPropagation.CallOriginal; // Always call the original desctructor!
                });
            return true;
        }

        #region Private

        private static ConstructorPatch<ObjectLifetimeObserver<T>> m_ConstructorPatch;
        private static DestructorPatch<ObjectLifetimeObserver<T>> m_DestructorPatch;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion
    }
}