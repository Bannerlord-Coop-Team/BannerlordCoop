using System;

namespace Coop.Mod.Patch
{
    public class MethodNotFoundException : Exception
    {
        public MethodNotFoundException(string msg) : base(msg)
        {
        }
    }

    public class InvokeFailedException : Exception
    {
        public InvokeFailedException(string msg) : base(msg)
        {
        }
    }

    public class FieldNotFoundException : Exception
    {
        public FieldNotFoundException(string msg) : base(msg)
        {
        }
    }
}
