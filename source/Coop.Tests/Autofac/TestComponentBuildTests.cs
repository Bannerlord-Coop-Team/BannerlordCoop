using Autofac;
using GameInterface.Services.Kingdoms;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Autofac;

public class TestComponentBuildTests
{
    private readonly ITestOutputHelper output;

    public TestComponentBuildTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ClientAndServerTestComponents_ResolveKingdomDecisionConverter()
    {
        using IContainer clientContainer = new ClientTestComponent(output).Container;
        using IContainer serverContainer = new ServerTestComponent(output).Container;

        Assert.NotNull(clientContainer.Resolve<IKingdomDecisionDataConverter>());
        Assert.NotNull(serverContainer.Resolve<IKingdomDecisionDataConverter>());
    }
}
