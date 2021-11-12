namespace BannerlordCoopInstaller
{
    class ResourceFile
    {
        private string fileName;
        private string fileExtension;
        private string installPath;
        public ResourceFile(string fileName, string fileExtension, string installPath)
        {
            this.fileName = fileName;
            this.fileExtension = fileExtension;
            this.installPath = installPath;
        }
        public string FileName{ get { return FileName; } set { FileName = value; } }
        public string FileExtension { get { return fileExtension; } set { fileExtension = value; } }
        public string InstallPath { get { return installPath; } set { installPath = value; } }
    }
}
