using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BannerlordSystemTestingLibrary
{
    [System.AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = true)]
    public class SystemTestingProtocolAttribute : Attribute { }

    [System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class ProtocolReceiverAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly Enum protocol;

        // This is a positional argument
        public ProtocolReceiverAttribute(Enum protocol)
        {
            this.protocol = protocol;
        }

        public Enum Protocol
        {
            get { return protocol; }
        }
    }

    [System.AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class ProtocolTransmitterAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly Enum protocol;

        // This is a positional argument
        public ProtocolTransmitterAttribute(Enum protocol)
        {
            this.protocol = protocol;
        }

        public Enum Protocol
        {
            get { return protocol; }
        }
    }
}
