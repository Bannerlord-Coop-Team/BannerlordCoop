using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace CoopMcpServer;

public interface IDeploymentFiles
{
    FileEvidence Inspect(string path);
    void Copy(string source, string target);
}

public sealed record FileEvidence(long Length, string Sha256, string Mvid);

public sealed class DeploymentFiles : IDeploymentFiles
{
    public FileEvidence Inspect(string path)
    {
        using var stream = File.OpenRead(path);
        long length = stream.Length;
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        string mvid = "";
        stream.Position = 0;
        if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) throw new InvalidDataException("Expected managed mod assembly: " + path);
            var metadata = pe.GetMetadataReader();
            mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString();
        }
        return new FileEvidence(length, hash, mvid);
    }

    public void Copy(string source, string target)
    {
        using var input = File.OpenRead(source);
        using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }
}
