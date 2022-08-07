using Common;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Data
{
    public abstract class WrapperBase<T> : IWrapper where T : class
    {
        public static event Action<IWrapper> OnCreated;

        public Guid Guid { get; }

        private WeakReference<T> _objectReference;

        internal T Object
        {
            get
            {
                if(_objectReference.TryGetTarget(out T target))
                {
                    return target;
                }
                throw new NullReferenceException();
            }
        }

        protected WrapperBase(T obj)
        {
            if(obj == null) throw new ArgumentNullException();
            _objectReference = new WeakReference<T>(obj);

            if (CoopObjectManager.TryGetGuid(obj, out Guid guid))
            {
                Guid = guid;
            }
            else
            {
                Guid = CoopObjectManager.RegisterNewObject(obj);
            }
        }

        protected WrapperBase(Guid guid)
        {
            Guid = guid;
            if (CoopObjectManager.TryGetObject(guid, out T obj))
            {
                _objectReference = new WeakReference<T>(obj);
            }
            else
            {
                throw new NullReferenceException();
            }
        }
    }
}