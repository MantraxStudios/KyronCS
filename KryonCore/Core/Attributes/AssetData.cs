using System;
namespace KrayonCore.Core.Attributes
{
    public class AssetRecord
    {
        public Guid Guid { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; }
    }
}