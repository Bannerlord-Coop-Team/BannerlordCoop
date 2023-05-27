using Coop.Core.Client;
using Coop.Core.Server;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests;

internal class TestEnvironment
{
    public ICoopServer Server { get; }
    public IEnumerable<ICoopClient> Clients { get; }

    public TestEnvironment()
    {
        
        builder.Services.AddHostedService<Worker>();
    }
}
