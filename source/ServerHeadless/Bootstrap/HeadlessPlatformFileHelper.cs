using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap
{
    /// <summary>
    /// Managed replacement for the native <see cref="IPlatformFileHelper"/>
    /// (<see cref="Common.PlatformFileHelper"/>), which the engine normally installs. Maps the
    /// engine's <see cref="PlatformFileType"/>-rooted virtual paths onto real filesystem paths so
    /// the save system can enumerate and read <c>.sav</c> files headlessly.
    ///
    /// <see cref="PlatformFileType.User"/> → Documents\Mount and Blade II Bannerlord (where the game
    /// stores "Game Saves").
    /// </summary>
    internal sealed class HeadlessPlatformFileHelper : IPlatformFileHelper
    {
        private readonly string _userRoot;
        private readonly string _applicationRoot;

        public HeadlessPlatformFileHelper(string applicationRoot)
        {
            _userRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord");
            _applicationRoot = applicationRoot;
        }

        private string Root(PlatformFileType type)
        {
            switch (type)
            {
                case PlatformFileType.User: return _userRoot;
                case PlatformFileType.Application: return _applicationRoot;
                case PlatformFileType.Temporary: return Path.GetTempPath();
                default: return _userRoot;
            }
        }

        private string ResolveDir(PlatformDirectoryPath path)
            => Path.Combine(Root(path.Type), path.Path ?? string.Empty);

        private string ResolveFile(PlatformFilePath path)
            => Path.Combine(ResolveDir(path.FolderPath), path.FileName ?? string.Empty);

        public string GetFileFullPath(PlatformFilePath filePath) => ResolveFile(filePath);

        public bool FileExists(PlatformFilePath path) => File.Exists(ResolveFile(path));

        public PlatformFilePath[] GetFiles(PlatformDirectoryPath path, string searchPattern, SearchOption searchOption)
        {
            string dir = ResolveDir(path);
            if (!Directory.Exists(dir)) return Array.Empty<PlatformFilePath>();

            return Directory.GetFiles(dir, searchPattern, searchOption)
                .Select(f => new PlatformFilePath(path, Path.GetFileName(f)))
                .ToArray();
        }

        public byte[] GetFileContent(PlatformFilePath filePath)
        {
            string full = ResolveFile(filePath);
            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }

        // The save metadata is a prefix of the .sav file; MetaData.Deserialize reads only what it
        // needs from the front of the stream, so returning the whole file is correct (if wasteful).
        public byte[] GetMetaDataContent(PlatformFilePath filePath) => GetFileContent(filePath);

        public string GetFileContentString(PlatformFilePath path)
        {
            string full = ResolveFile(path);
            return File.Exists(full) ? File.ReadAllText(full) : null;
        }

        public Task<string> GetFileContentStringAsync(PlatformFilePath path)
            => Task.FromResult(GetFileContentString(path));

        public bool DeleteFile(PlatformFilePath path)
        {
            string full = ResolveFile(path);
            if (!File.Exists(full)) return false;
            File.Delete(full);
            return true;
        }

        public SaveResult SaveFile(PlatformFilePath path, byte[] data)
        {
            string full = ResolveFile(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, data);
            return SaveResult.Success;
        }

        public SaveResult SaveFileString(PlatformFilePath path, string data)
        {
            string full = ResolveFile(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, data);
            return SaveResult.Success;
        }

        public SaveResult AppendLineToFileString(PlatformFilePath path, string data)
        {
            string full = ResolveFile(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.AppendAllText(full, data + Environment.NewLine);
            return SaveResult.Success;
        }

        public Task<SaveResult> SaveFileAsync(PlatformFilePath path, byte[] data)
            => Task.FromResult(SaveFile(path, data));

        public Task<SaveResult> SaveFileStringAsync(PlatformFilePath path, string data)
            => Task.FromResult(SaveFileString(path, data));

        public string GetError() => string.Empty;
    }
}
