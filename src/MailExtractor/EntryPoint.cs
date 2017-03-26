using System;
using System.Data;
using System.Threading;
using Common;
using Dapper;

namespace MailExtractor
{
    /// <summary>
    /// The mail extractor continously runs in the background to provide timely extraction of incoming mails enqueued by the MX SMTP server.
    /// 
    /// Configuration.Instance.ExtractedMailFolder -> /tmp/mailboxes
    /// 
    /// /tmp/mailboxes/mbox_token
    ///     mails.json - list of Common.MailEntry
    /// 
    /// /tmp/mailboxes/mbox_token/mail_id
    ///     body.html - mail body
    ///     png/jpg/etc - inline images
    ///     attachments/file1.pdf - attachment in subfolders
    /// </summary>
    public sealed class EntryPoint
    {
        public static void Main(string[] args)
        {
            try
            {
                var loadConfig = Configuration.Instance;
                new EntryPoint().Run();
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }
        }

        public void Run()
        {
            while (true)
            {
                try { ExtractIncomingMails(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
                Thread.Sleep(5 * 1000);
            }
        }

        private void ExtractIncomingMails()
        {
            using (var c = DB.CreateConnection())
            {
                IncomingMailDTO m;
                c.Open();
                while ((m = GetOldestIncomingMail(c)) != null)
                {
                    try { ExtractIncomingMail(c, m); } catch { }
                    DeleteIncomingMail(c, m);
                }
            }
        }

        private IncomingMailDTO GetOldestIncomingMail(IDbConnection c)
        {
            return c.QueryFirstOrDefault<IncomingMailDTO>("select Id,ReceivedOn,Recipient,Sender,ContentSize,ContentId from IncomingMail order by Id limit 1");
        }

        private void DeleteIncomingMail(IDbConnection c, IncomingMailDTO m)
        {
            c.Execute("delete from IncomingMail where Id=@Id", m);
            m.DeleteContentFile();
        }

        private void ExtractIncomingMail(IDbConnection c, IncomingMailDTO m)
        {
            new Extractor(c, m).Extract();
        }
    }
}
