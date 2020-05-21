using System;
using Coop.Network;
using JetBrains.Annotations;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Mod
{
    public class SaveGameData
    {
        public SaveGameData([NotNull] MetaData metaData, [NotNull] SaveOutput saveOutput)
        {
            MetaData = metaData;
            SaveOutput = saveOutput;
        }

        private MetaData MetaData { get; }
        private SaveOutput SaveOutput { get; }

        public void Serialize(ByteWriter writer)
        {
            MetaData.Serialize(writer.Binary.BaseStream);
            writer.Binary.Write(SaveOutput.Data.GetData());
        }

        public override string ToString()
        {
            string sRet;
            sRet = $"[MetaData]: {MetaData.Count} entries.";
            foreach (string key in MetaData.Keys)
            {
                if (MetaData.TryGetValue(key, out string value))
                {
                    sRet += Environment.NewLine + $"{key} := {MetaData[key]}";
                }
            }

            sRet += Environment.NewLine +
                    "[GameData]: " +
                    (SaveOutput.Successful ? "Success" : "Failure");
            if (SaveOutput.Successful)
            {
                sRet += Environment.NewLine;
                sRet +=
                    $"Total: {SaveOutput.Data.TotalSize} bytes ({SaveOutput.Data.GetData().Length}). ";
                sRet += Environment.NewLine;
                sRet += $"Header: {SaveOutput.Data.Header.Length} bytes. ";
                sRet += Environment.NewLine;
                sRet += $"ContainerData: {SaveOutput.Data.ContainerData.Length} bytes.";
                return sRet;
            }

            sRet = ". Errors during save:";
            for (int i = 0; i < SaveOutput.Errors.Length; i++)
            {
                sRet += Environment.NewLine + $"[{i}] {SaveOutput.Errors[i]}";
            }

            return sRet;
        }
    }
}
