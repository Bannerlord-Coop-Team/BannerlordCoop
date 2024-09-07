using GameInterface.AutoSync.Builders;

namespace GameInterface.AutoSync;

public interface IPacketSwitchProvider
{
    ITypeSwitcher Switcher { get; set; }
}

class PacketSwitchProvider : IPacketSwitchProvider
{
    public ITypeSwitcher Switcher { get; set; }
}
