using System.Reflection;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Heroes;

internal class CoopInMemSaveDriver : InMemDriver
{
    private static readonly FieldInfo _data = typeof(InMemDriver).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

    public CoopInMemSaveDriver()
    {
    }

    public CoopInMemSaveDriver(byte[] saveData)
    {
        _data.SetValue(this, saveData);
    }

    public byte[] Data 
    { 
        get 
        { 
            return (byte[])_data.GetValue(this);
        }
    }
}
