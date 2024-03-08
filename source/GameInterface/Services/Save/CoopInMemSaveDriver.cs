using System.Reflection;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Heroes;

internal class CoopInMemSaveDriver : InMemDriver
{

    public CoopInMemSaveDriver()
    {
    }

    public CoopInMemSaveDriver(byte[] saveData)
    {
        _data = saveData;
    }

    public byte[] Data 
    { 
        get 
        { 
            return _data;
        }
    }
}
