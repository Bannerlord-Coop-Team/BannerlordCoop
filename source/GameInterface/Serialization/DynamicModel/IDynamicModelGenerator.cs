using System;
using System.Collections.Generic;

namespace GameInterface.Serialization.DynamicModel
{
    public interface IDynamicModelGenerator
    {
        void AssignSurrogate<TClass, TSurrogate>();
        void Compile();
        void CreateDynamicSerializer<T>(IEnumerable<Type> exclude = null);
    }
}