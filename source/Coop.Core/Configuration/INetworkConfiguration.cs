namespace Coop.Core.Configuration
{
    public interface INetworkConfiguration
    {
        string Address { get; }
        int Port { get; }
        string Token { get; }
        string P2PToken { get; }
    }
}