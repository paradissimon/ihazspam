using System;
using System.IO;

namespace Common
{
    public sealed class IncomingMailDTO
    {
        public int Id { get; set; }
        public DateTime ReceivedOn { get; set; }
        public string Recipient { get; set; }
        public string Sender { get; set; }
        public int ContentSize { get; set; }
        public Guid ContentId { get; set; }

        public IncomingMailDTO() : this(DateTime.UtcNow, String.Empty, string.Empty, 0, Guid.Empty)
        {
        }

        public IncomingMailDTO(DateTime receivedOnUtc, string recipient, string sender, int contentSize, Guid contentId)
        {
            Id = 0;
            ReceivedOn = receivedOnUtc;
            Recipient = recipient;
            Sender = sender;
            ContentSize = contentSize;
            ContentId = contentId;
        }

        public string GetContentFileName()
        {
            return GetContentFileName(ContentId);
        }

        public void DeleteContentFile()
        {
            DeleteContentFile(ContentId);
        }

        public override string ToString()
        {
            return string.Format("[Recipient={0}, Sender={1}, ContentSize={2}, ContentId={3}]", Recipient, Sender, ContentSize, ContentId);
        }

        public static string GetContentFileName(Guid contentId)
        {
            return Path.Combine(Configuration.Instance.IncomingMailDirectory, contentId.ToString());
        }

        public static void DeleteContentFile(Guid contentId)
        {
            File.Delete(GetContentFileName(contentId));
        }
    }
}
