using System;
using System.IO;

namespace Coop.CrashReporter
{
    internal static class MinidumpProcessIdReader
    {
        private const uint MinidumpSignature = 0x504D444D;
        private const uint MiscInfoStreamType = 15;
        private const uint MiscInfoProcessIdFlag = 0x00000001;
        private const int HeaderSize = 32;
        private const int DirectoryEntrySize = 12;
        private const int MinimumMiscInfoSize = 12;
        private const uint MaximumStreamCount = 65536;

        public static bool TryReadProcessId(string dumpPath, out int processId)
        {
            processId = 0;

            try
            {
                using (var stream = new FileStream(
                    dumpPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < HeaderSize || reader.ReadUInt32() != MinidumpSignature)
                        return false;

                    reader.ReadUInt32();
                    uint streamCount = reader.ReadUInt32();
                    uint directoryRva = reader.ReadUInt32();
                    if (streamCount > MaximumStreamCount ||
                        !IsRangeInsideFile(stream, directoryRva, (long)streamCount * DirectoryEntrySize))
                    {
                        return false;
                    }

                    for (uint index = 0; index < streamCount; index++)
                    {
                        stream.Position = directoryRva + ((long)index * DirectoryEntrySize);
                        uint streamType = reader.ReadUInt32();
                        uint dataSize = reader.ReadUInt32();
                        uint dataRva = reader.ReadUInt32();
                        if (streamType != MiscInfoStreamType ||
                            dataSize < MinimumMiscInfoSize ||
                            !IsRangeInsideFile(stream, dataRva, MinimumMiscInfoSize))
                        {
                            continue;
                        }

                        stream.Position = dataRva;
                        uint sizeOfInfo = reader.ReadUInt32();
                        uint flags = reader.ReadUInt32();
                        uint dumpProcessId = reader.ReadUInt32();
                        if (sizeOfInfo < MinimumMiscInfoSize ||
                            (flags & MiscInfoProcessIdFlag) == 0 ||
                            dumpProcessId == 0 ||
                            dumpProcessId > int.MaxValue)
                        {
                            return false;
                        }

                        processId = (int)dumpProcessId;
                        return true;
                    }
                }
            }
            catch (EndOfStreamException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return false;
        }

        private static bool IsRangeInsideFile(FileStream stream, long offset, long length)
        {
            return offset >= 0 &&
                   length >= 0 &&
                   offset <= stream.Length &&
                   length <= stream.Length - offset;
        }
    }
}
