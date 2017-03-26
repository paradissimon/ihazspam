using System;
using System.Collections.Generic;

namespace Common
{
    /// <summary>
    /// JSON mail entry (emails.json)
    /// </summary>
    public sealed class MailEntry
    {
        public sealed class Attachment
        {
            public string OriginalFileName { get; set; }
            public string FileName { get; set; }
        }

        public int Id { get; set; } = 0;
        public string Sender { get; set; } = string.Empty;
        public DateTime ReceivedOn { get; set; } = DateTime.MinValue;
        public string Subject { get; set; } = string.Empty;
        public int Size { get; set; } = 0;
        public List<Attachment> Attachments { get; } = new List<Attachment>();
    }
}
