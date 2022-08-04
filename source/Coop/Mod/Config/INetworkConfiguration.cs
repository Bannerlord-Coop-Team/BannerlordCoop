namespace Coop.Mod
{
    public interface INetworkConfiguration
    {
        string LanAddress { get; }
        int LanPort { get; }
    }
}