using System;
using System.Linq;
using JetBrains.Annotations;
using NLog;
using Sync;
using Sync.Behaviour;

namespace CoopFramework
{
    public class ObjectLifetimeObserver<T> : IObjectLifetimeObserver where T : class
    {
        public Action<T> OnAfterCreateObject;
        public Action<T> OnAfterRemoveObject;
        
        public void AfterRegisterObject(object createdObject)
        {
            if (!(createdObject is T instance))
            {
                throw new Exception("Unexpected object type.");
            }
            OnAfterCreateObject?.Invoke(instance);
        }

        public void AfterUnregisterObject(object removedObject)
        {
            if (!(removedObject is T instance))
            {
                throw new Exception("Unexpected object type.");
            }
            OnAfterRemoveObject?.Invoke(instance);
        }

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
        [CanBeNull] private static ConstructorPatch<ObjectLifetimeObserver<T>> m_ConstructorPatch;
        [CanBeNull] private static DestructorPatch<ObjectLifetimeObserver<T>> m_DestructorPatch;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        #endregion
    }
}