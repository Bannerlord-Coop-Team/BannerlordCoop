using GameInterface.Utils;
using Xunit;

namespace Coop.Tests.GameInterface.Utils;

public class WindowTitleTests
{
    [Fact]
    public void Server_role_labels_as_server()
    {
        Assert.Equal("Coop Server", WindowTitle.LabelFor(isServer: true, platformId: "testclient1"));
    }

    [Theory]
    [InlineData("testclient1", "Coop Client 1")]
    [InlineData("testclient2", "Coop Client 2")]
    [InlineData("TestClient2", "Coop Client 2")]
    [InlineData("testclient", "Coop Client")]
    public void Client_label_strips_testclient_prefix(string platformId, string expected)
    {
        Assert.Equal(expected, WindowTitle.LabelFor(isServer: false, platformId));
    }

    [Fact]
    public void Client_with_null_platform_id_labels_as_generic_client()
    {
        Assert.Equal("Coop Client", WindowTitle.LabelFor(isServer: false, platformId: null));
    }

    [Fact]
    public void Client_with_empty_platform_id_labels_as_generic_client()
    {
        Assert.Equal("Coop Client", WindowTitle.LabelFor(isServer: false, platformId: ""));
    }

    [Fact]
    public void Client_with_unrecognized_platform_id_keeps_it()
    {
        Assert.Equal("Coop Client steamProfileA", WindowTitle.LabelFor(isServer: false, "steamProfileA"));
    }
}
