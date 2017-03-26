using System;
using System.Collections.Generic;
using Common;

namespace MailCleaner
{
    public class EntryPoint
    {
        public static void Main(string[] args)
        {
            try
            {
                var loadConfig = Configuration.Instance;
                var cleaner = new Cleaner();

                IList<Guid> contentIds, tokens;
                using (var c = DB.CreateConnection())
                {
                    c.Open();
                    contentIds = cleaner.PurgeOldIncomingMails(c);
                    tokens = cleaner.PurgeExpiredMailboxes(c);
                }
                cleaner.PurgeOldIncomingMailsFiles(contentIds);
                cleaner.PurgeExpiredMailboxesDirectories(tokens);                
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }
        }
    }
}
