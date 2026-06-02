namespace Ventoy_Builder.Models
{
    public class IsoLibraryItem
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Category { get; set; } = "";
        public string SizeText { get; set; } = "";
        public long SizeBytes { get; set; }

        public string DisplayName { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string DetectedType { get; set; } = "";
    }
}