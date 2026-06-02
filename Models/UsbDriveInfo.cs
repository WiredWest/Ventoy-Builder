namespace Ventoy_Builder.Models
{
    public class UsbDriveInfo
    {
        public string DriveLetter { get; set; } = "";
        public string Label { get; set; } = "";
        public string SizeText { get; set; } = "";
        public string FreeSpaceText { get; set; } = "";
        public string FileSystem { get; set; } = "";
        public string FullPath { get; set; } = "";

        public long TotalSizeBytes { get; set; }
        public long FreeSpaceBytes { get; set; }

        public bool IsRemovable { get; set; }
        public bool IsReady { get; set; }
    }
}