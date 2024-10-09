using GameInterface.AutoSync.Fields;
using GameInterface.AutoSync.Properties;

namespace GameInterface.AutoSync;

public interface IPacketSwitchProvider
{
    IFieldTypeSwitcher FieldSwitch { get; set; }
    IPropertyTypeSwitcher PropertySwitch { get; set; }
}

class PacketSwitchProvider : IPacketSwitchProvider
{
    public IFieldTypeSwitcher FieldSwitch { get; set; }
    public IPropertyTypeSwitcher PropertySwitch { get; set; }
}
