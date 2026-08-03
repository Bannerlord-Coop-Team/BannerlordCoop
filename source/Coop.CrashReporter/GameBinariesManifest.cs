using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Coop.CrashReporter
{
    /// <summary>
    /// Writes a JSON manifest of every file in the game binaries directory
    /// (bin/Win64_Shipping_Client) together with its version, size and last write time.
    /// A dump does not say which build, which DLC or which mixture of hand-copied game files
    /// was actually installed, so the manifest is what tells a broken install apart from a real bug.
    /// </summary>
    internal static class GameBinariesManifest
    {
        internal const string FileName = "game-binaries.json";

        // The interesting files are the game's own binaries and the runtime folders next to them
        // (bin/Win64_Shipping_Client/Microsoft.NETCore.App/<version>). Walking everything instead
        // pulls in the bundled mono tree - 4700 files whose versions no one has ever needed, and a
        // minute and a half of disk on a cold cache right after the game died.
        internal const int MaximumDirectoryDepth = 2;

        // Bounds the manifest if the directory ever contains something unexpected, e.g. an
        // unpacked asset dump; a full game install lists well under a thousand files.
        private const int MaximumFileCount = 20000;

        /// <summary>
        /// Writes the manifest and returns a one-line summary for the report. <paramref name="written"/>
        /// is true when the manifest file exists on disk afterwards.
        /// </summary>
        internal static string Write(
            string manifestPath,
            string binariesDirectory,
            out bool written)
        {
            written = false;
            if (string.IsNullOrEmpty(binariesDirectory))
                return "not captured (directory was not resolved)";
            if (!Directory.Exists(binariesDirectory))
                return "not captured (directory does not exist: " + binariesDirectory + ")";

            string root = binariesDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            int skippedDirectories;
            bool truncated;
            List<FileEntry> files = ReadFiles(root, out skippedDirectories, out truncated);

            try
            {
                using (var stream = new FileStream(
                    manifestPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    WriteManifest(writer, root, files, skippedDirectories, truncated);
                }
            }
            catch (IOException)
            {
                return "not captured (manifest could not be written)";
            }
            catch (UnauthorizedAccessException)
            {
                return "not captured (manifest could not be written)";
            }

            written = true;
            var summary = new StringBuilder();
            summary.Append(files.Count.ToString(CultureInfo.InvariantCulture));
            summary.Append(" files listed in ").Append(FileName);
            if (skippedDirectories > 0)
            {
                summary.Append(", ")
                    .Append(skippedDirectories.ToString(CultureInfo.InvariantCulture))
                    .Append(" deeper directories not listed");
            }

            if (truncated)
                summary.Append(" (truncated)");

            return summary.ToString();
        }

        private static List<FileEntry> ReadFiles(
            string root,
            out int skippedDirectories,
            out bool truncated)
        {
            var entries = new List<FileEntry>();
            var pending = new Stack<PendingDirectory>();
            pending.Push(new PendingDirectory(root, 0));
            skippedDirectories = 0;
            truncated = false;

            while (pending.Count > 0 && !truncated)
            {
                PendingDirectory current = pending.Pop();
                foreach (string subdirectory in TryGetDirectories(current.Path))
                {
                    if (current.Depth < MaximumDirectoryDepth)
                        pending.Push(new PendingDirectory(subdirectory, current.Depth + 1));
                    else
                        skippedDirectories++;
                }

                foreach (string file in TryGetFiles(current.Path))
                {
                    if (entries.Count >= MaximumFileCount)
                    {
                        truncated = true;
                        break;
                    }

                    FileEntry entry = TryReadEntry(root, file);
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            entries.Sort((left, right) => string.Compare(
                left.RelativePath,
                right.RelativePath,
                StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        // An unreadable subdirectory must cost its own contents, not the whole manifest.
        private static string[] TryGetDirectories(string directory)
        {
            try
            {
                return Directory.GetDirectories(directory);
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        private static string[] TryGetFiles(string directory)
        {
            try
            {
                return Directory.GetFiles(directory);
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        private static FileEntry TryReadEntry(string root, string path)
        {
            try
            {
                var info = new FileInfo(path);
                var entry = new FileEntry
                {
                    RelativePath = GetRelativePath(root, path),
                    Length = info.Length,
                    LastWriteUtc = info.LastWriteTimeUtc,
                };

                if (HasVersionResource(path))
                {
                    FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                    entry.FileVersion = NullIfEmpty(version.FileVersion);
                    entry.ProductVersion = NullIfEmpty(version.ProductVersion);
                }

                return entry;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool HasVersionResource(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string GetRelativePath(string root, string path)
        {
            string relative = path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length)
                : path;
            return relative
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void WriteManifest(
            TextWriter writer,
            string directory,
            List<FileEntry> files,
            int skippedDirectories,
            bool truncated)
        {
            writer.WriteLine("{");
            writer.WriteLine("  \"capturedUtc\": " +
                Quote(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)) + ",");
            writer.WriteLine("  \"directory\": " + Quote(directory) + ",");
            writer.WriteLine("  \"fileCount\": " +
                files.Count.ToString(CultureInfo.InvariantCulture) + ",");
            writer.WriteLine("  \"maximumDirectoryDepth\": " +
                MaximumDirectoryDepth.ToString(CultureInfo.InvariantCulture) + ",");
            writer.WriteLine("  \"skippedDirectories\": " +
                skippedDirectories.ToString(CultureInfo.InvariantCulture) + ",");
            writer.WriteLine("  \"truncated\": " + (truncated ? "true" : "false") + ",");
            writer.WriteLine("  \"files\": [");

            var line = new StringBuilder();
            for (var index = 0; index < files.Count; index++)
            {
                FileEntry file = files[index];
                line.Length = 0;
                line.Append("    { \"path\": ").Append(Quote(file.RelativePath));
                line.Append(", \"bytes\": ")
                    .Append(file.Length.ToString(CultureInfo.InvariantCulture));
                line.Append(", \"modifiedUtc\": ").Append(
                    Quote(file.LastWriteUtc.ToString("O", CultureInfo.InvariantCulture)));
                if (file.FileVersion != null)
                    line.Append(", \"fileVersion\": ").Append(Quote(file.FileVersion));
                if (file.ProductVersion != null)
                    line.Append(", \"productVersion\": ").Append(Quote(file.ProductVersion));
                line.Append(index == files.Count - 1 ? " }" : " },");
                writer.WriteLine(line.ToString());
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u").Append(
                                ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private sealed class PendingDirectory
        {
            public PendingDirectory(string path, int depth)
            {
                Path = path;
                Depth = depth;
            }

            public readonly string Path;
            public readonly int Depth;
        }

        private sealed class FileEntry
        {
            public string RelativePath;
            public long Length;
            public DateTime LastWriteUtc;
            public string FileVersion;
            public string ProductVersion;
        }
    }
}
