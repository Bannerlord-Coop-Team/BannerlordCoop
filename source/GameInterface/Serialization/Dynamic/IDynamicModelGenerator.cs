using System;
using System.Collections.Generic;

namespace GameInterface.Serialization.Dynamic
{
    public interface IDynamicModelGenerator
    {
        void AssignSurrogate<TClass, TSurrogate>();
        void Compile();
        void CreateDynamicSerializer<T>(string[] exclude = null);
    }
}