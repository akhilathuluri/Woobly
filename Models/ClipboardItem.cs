using System;

namespace Woobly.Models
{
    public class ClipboardItem
    {
        public string? Content { get; set; }
        public DateTime CopiedAt { get; set; } = DateTime.Now;
        public string? Preview => Content?.Length > 100 
            ? Content.Substring(0, 100) + "..." 
            : Content;
    }
}
