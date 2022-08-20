using Coop.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class CoopTest
    {
        public readonly MessageBrokerStub messageBroker;
        public readonly ITestOutputHelper output;

        public CoopTest(ITestOutputHelper output)
        {
            this.output = output;
            messageBroker = new MessageBrokerStub();
        }
    }
}
