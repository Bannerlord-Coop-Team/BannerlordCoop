using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Components
{

    public interface IComponentContainer
    {
        IEnumerable<IComponent> Components { get; }
        bool AddComponent<T>(T component) where T : IComponent;
        T GetComponent<T>() where T : class, IComponent;
        bool RemoveComponent<T>(T component) where T : IComponent;
    }
}
