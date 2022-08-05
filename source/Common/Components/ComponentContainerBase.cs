using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Components
{
    public abstract class ComponentContainerBase : IComponentContainer
    {
        public IEnumerable<IComponent> Components => _components.Values;
        private readonly Dictionary<Type, IComponent> _components = new Dictionary<Type, IComponent>();

        public bool AddComponent<T>(T component) where T : IComponent
        {
            if (_components.ContainsKey(component.GetType())) return false;

            _components.Add(component.GetType(), component);
            return true;
        }

        private Type GetInterface(Type t)
        {
            if(t.IsInterface) return t;

            if (t.BaseType == null) return null;

            return GetInterface(t.BaseType);
        }

        public bool RemoveComponent<T>(T component) where T : IComponent
        {
            if (_components.ContainsKey(component.GetType()) == false) return false;

            _components.Remove(component.GetType());
            return true;
        }

        public T GetComponent<T>() where T : class, IComponent
        {
            return _components[typeof(T)] as T;
        }
    }
}
