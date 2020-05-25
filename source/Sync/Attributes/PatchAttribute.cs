using System;

namespace Sync.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class PatchAttribute : Attribute
    {
    }
}
