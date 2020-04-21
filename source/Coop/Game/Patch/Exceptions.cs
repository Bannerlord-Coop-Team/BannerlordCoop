using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Game.Patch
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
