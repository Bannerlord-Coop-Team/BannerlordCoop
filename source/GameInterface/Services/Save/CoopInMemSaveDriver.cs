using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using TaleWorlds.Library;
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

/// <summary>Produces a native FileDriver-format campaign save without writing to the user save directory.</summary>
internal class CoopFileInMemSaveDriver : ISaveDriver
{
    private byte[] data = Array.Empty<byte>();

    public byte[] Data => data;

    public Task<SaveResultWithMessage> Save(string saveName, int version, MetaData metaData, GameData gameData)
    {
        using var stream = new MemoryStream();
        metaData.Add("Version", version.ToString());
        metaData.Serialize(stream);
        using (var output = new DeflateStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new System.IO.BinaryWriter(output))
        {
            GameData.Write(writer, gameData);
        }

        data = stream.ToArray();
        return Task.FromResult(SaveResultWithMessage.Default);
    }

    public MetaData LoadMetaData(string saveName)
    {
        using var stream = new MemoryStream(data);
        return MetaData.Deserialize(stream);
    }

    public LoadData Load(string saveName)
    {
        using var stream = new MemoryStream(data);
        var metaData = MetaData.Deserialize(stream);
        using var input = new DeflateStream(stream, CompressionMode.Decompress);
        using var reader = new System.IO.BinaryReader(input);
        return new LoadData(metaData, GameData.Read(reader));
    }

    public SaveGameFileInfo[] GetSaveGameFileInfos() => Array.Empty<SaveGameFileInfo>();

    public string[] GetSaveGameFileNames() => Array.Empty<string>();

    public bool Delete(string saveName)
    {
        data = Array.Empty<byte>();
        return true;
    }

    public bool IsSaveGameFileExists(string saveName) => false;

    public bool IsWorkingAsync() => false;
}
