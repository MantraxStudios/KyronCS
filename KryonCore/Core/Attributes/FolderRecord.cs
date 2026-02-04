using System;

namespace KrayonCore.Core.Attributes
{
    public class FolderRecord
    {
        public Guid Guid { get; set; }
        public string Path { get; set; }
        public DateTime CreatedAt { get; set; }

        public FolderRecord()
        {
            Guid = Guid.NewGuid();
            CreatedAt = DateTime.Now;
        }
    }
}