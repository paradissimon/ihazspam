using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Common;
using Dapper;

namespace MailCleaner
{
    public sealed class Cleaner
    {
        /// <summary>
        /// Removes old incoming mails files that may have been forgotton to due crashes. 
        /// Any mail stuck in the incoming mail table or file for more than NN hours is zapped.
        /// </summary>
        public IList<Guid> PurgeOldIncomingMails(IDbConnection c)
        {
            var dt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(6));
            var contentIds = c.Query<Guid>("delete from IncomingMail where ReceivedOn < @Threshold returning ContentId;", new { Threshold = dt }).AsList();
            return contentIds;
        }


        /// <summary>
        /// Archives expired mailbox, keeping only basic statistics for eventual usage reporting.
        /// Note: the expired mailbox table support duplicate email addresses if that ever happens.
        /// </summary>
        public IList<Guid> PurgeExpiredMailboxes(IDbConnection c)
        {
            // Archive
            var dt = DateTime.UtcNow;
            var crlf = "\r\n";            
            var sql =
                "create local temporary table expired_mbox(id int, token uuid);" + crlf +
                "insert into expired_mbox(id,token) select Id, Token from MailBox where ExpiresOn < @Now;" + crlf +
                "insert into ExpiredMailBox(Id, Token, Address, ExpiresOn) select Id, Token, Address, ExpiresOn from MailBox where Id in (select id from expired_mbox);" + crlf +
                "insert into ExpiredMail(Id, IdMailBox, ReceivedOn, Size) select Id, IdMailBox, ReceivedOn, Size from Mail where IdMailBox in (select id from expired_mbox);" + crlf +
                "delete from Mail where IdMailBox in (select id from expired_mbox);" + crlf +
                "delete from MailBox where Id in (select id from expired_mbox);";
            using (var tx = c.BeginTransaction())
            {
                c.Execute(sql, new { Now = dt }, transaction: tx);
                tx.Commit();
            }
            var tokens = c.Query<Guid>("select token from expired_mbox;").AsList();
            c.Execute("drop table expired_mbox;");

            return tokens;
        }
        

        public void PurgeOldIncomingMailsFiles(IList<Guid> contentIds)
        {
            foreach (var contentId in contentIds)
            {
                try { IncomingMailDTO.DeleteContentFile(contentId); } catch { }
            }

            // Drop any missed mail folders
            var zombieThreshold = DateTime.UtcNow.AddMinutes(-2 * Configuration.Instance.TimeToLiveInMinutes);
            var incomingMailDirectory = new DirectoryInfo(Configuration.Instance.IncomingMailDirectory);
            foreach (var incomingMail in incomingMailDirectory.GetFiles())
            {
                if (incomingMail.LastWriteTimeUtc < zombieThreshold)
                {
                    try { File.Delete(incomingMail.FullName); } catch { }
                }
            }
        }


        public void PurgeExpiredMailboxesDirectories(IList<Guid> expiredTokens)
        {
            foreach (var token in expiredTokens)
            {
                var mbox = Path.Combine(Configuration.Instance.MailboxDirectory, token.ToString());
                try { Directory.Delete(mbox, true); } catch { }
            }

            // Drop any missed mail folders
            var zombieThreshold = DateTime.UtcNow.AddMinutes(-2 * Configuration.Instance.TimeToLiveInMinutes);
            var mailboxDirectory = new DirectoryInfo(Configuration.Instance.MailboxDirectory);
            foreach (var mbox in mailboxDirectory.EnumerateDirectories())
            {
                if (mbox.LastWriteTimeUtc < zombieThreshold)
                {
                    try { Directory.Delete(mbox.FullName, true); } catch { }
                }
            }
        }
    }
}
