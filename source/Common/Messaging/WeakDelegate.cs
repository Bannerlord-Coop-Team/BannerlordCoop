using System;
using System.Reflection;

namespace Common.Messaging
{
    /// <summary>
    /// Delegate that does not hold a strong reference to the target
    /// </summary>
    public class WeakDelegate
    {
        public bool IsAlive => reference.IsAlive;

        private WeakReference reference;
        private MethodInfo method;

        public WeakDelegate(Delegate @delegate)
        {
            reference = new WeakReference(@delegate.Target);
            method = @delegate.Method;
        }

        public void Invoke(object[] parameters)
        {
            var obj = reference.Target;

            if (obj == null) return;

            method.Invoke(obj, parameters);
        }

        public override bool Equals(object obj)
        {
            if (obj is WeakDelegate weakDelegate == false) return false;

            if (weakDelegate.method != method) return false;
            if (weakDelegate.reference.Target != reference.Target) return false;

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private Delegate ToDelegate()
        {
            return Delegate.CreateDelegate(reference.Target.GetType(), method);
        }

        public static implicit operator WeakDelegate(Delegate d) => new WeakDelegate(d);
        public static implicit operator Delegate(WeakDelegate d) => d.ToDelegate();
    }
}
