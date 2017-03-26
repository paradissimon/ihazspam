using System;
using System.Text;

namespace Common
{
    public sealed class MailboxDTO
    {
        private static readonly char[] Set1 = new[] { 'a', 'e', 'i', 'o', 'u', 'y' };
        private static readonly char[] Set2 = new[] { 'b', 'c', 'd', 'f', 'l', 'm', 'n', 'p', 'r', 's', 't', 'v' };

        public int Id { get; set; }
        public Guid Token { get; set; }
        public string Address { get; set; }
        public DateTime ExpiresOn { get; set; }

        public bool IsExpired()
        {
            return DateTime.UtcNow >= ExpiresOn;
        }

        public static MailboxDTO CreateRandomOne()
        {
            var rng = new Random();
            var domains = Configuration.Instance.MailDomains;
            
            var m = new MailboxDTO();
            m.Token = Guid.NewGuid();
            m.Address = RandomName() + '@' + domains[rng.Next() % domains.Count];
            m.ExpiresOn = DateTime.UtcNow.AddMinutes(Configuration.Instance.TimeToLiveInMinutes);
            return m;
        }

        public static string RandomName()
        {
            var firstNamelen = 6;
            var fullNameLen = 14;
            var sb = new StringBuilder(fullNameLen + 1);
            var rng = new Random();
            for (var n = 1; n <= fullNameLen; n++)
            {
                var set = (n % 2 == 0) ? Set1 : Set2;
                sb.Append(set[rng.Next() % set.Length]);
                if (n == firstNamelen) sb.Append('.');
            }
            return sb.ToString();
        }
    }
}
