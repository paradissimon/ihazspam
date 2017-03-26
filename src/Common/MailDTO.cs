using System;

namespace Common
{
    public class MailDTO
    {
        public int Id { get; set; }
        public int IdMailBox { get; set; }
        public DateTime ReceivedOn { get; set; }
        public int Size { get; set; }
    }
}
