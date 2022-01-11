namespace BannerlordCoopInstaller
{
    /// <summary>
    /// Store additional information about resources such as their extension and install path.
    /// </summary>
    class ResourceFile
    {
        private string fileExtension;
        private string installPath;
        public ResourceFile(string fileExtension, string installPath)
        {
            this.fileExtension = fileExtension;
            this.installPath = installPath;
        }
        public string FileExtension { get { return fileExtension; } set { fileExtension = value; } }
        public string InstallPath { get { return installPath; } set { installPath = value; } }
    }
}
