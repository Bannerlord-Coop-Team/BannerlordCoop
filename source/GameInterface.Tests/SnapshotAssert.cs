using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests
{
    public class SnapshotAssert
    {

        public static void Equals(string snapshot, [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "")
        {
            var directory = Path.GetDirectoryName(filePath);
            var snapshotDirectory = Path.Combine(directory, "Snapshots");
            var snapshotPath = Path.Combine(snapshotDirectory, Path.GetFileNameWithoutExtension(filePath) + "." + memberName + ".snapshot");

            if (!Directory.Exists(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            if (!Directory.Exists(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            if (!File.Exists(snapshotPath))
            {
                File.WriteAllText(snapshotPath, snapshot);
            }
            var expectedSnapshot = File.ReadAllText(snapshotPath);

            Assert.Equal(expectedSnapshot, snapshot);
        }
    }
}
