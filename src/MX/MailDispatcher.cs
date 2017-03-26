using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using Common;
using Dapper;

namespace MX
{
    /// <summary>
    /// The MailDispatcher thrad maintains an internal cache of active mailbox. The SMTP server refuse incoming mails to inactive addresses.
    /// When a mail is accepted, its content gets stored on the filesystem but associated meta-data is kept in the IncomingMail table.
    /// The MailExtractor processes that queue. An unbounded inmemory blocking consumer-producer queue act as a buffer between
    /// mail reception and meta-data writing to the database.
    /// </summary>
    public sealed class MailDispatcher
    {
        private readonly BlockingCollection<IncomingMailDTO> _queue;
        private Thread _dispatcher;

        private readonly ReaderWriterLockSlim _activeMailBoxCacheLock;
        private List<string> _activeMailBoxCache;
        private Thread _activeMailBoxCacheUpdater;


        public MailDispatcher()
        {
            _queue = new BlockingCollection<IncomingMailDTO>();
            _activeMailBoxCacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _activeMailBoxCache = new List<string>();
        }

        public void Start()
        {
            if (_dispatcher == null)
            {
                _dispatcher = new Thread(IncomingMailDispatcherThread);
                _dispatcher.Name = "Mail Dispatcher Thread";
                _dispatcher.IsBackground = false;
                _dispatcher.Start();
            }

            if (_activeMailBoxCacheUpdater == null)
            {
                _activeMailBoxCacheUpdater = new Thread(ActiveMailboxUpdaterThread);
                _activeMailBoxCacheUpdater.Name = "Active Mailbox Updater Thread";
                _activeMailBoxCacheUpdater.IsBackground = false;
                _activeMailBoxCacheUpdater.Start();
            }
        }

        public void Enqueue(IncomingMailDTO m)
        {
            _queue.Add(m);
        }

        public bool IsMailboxActive(string recipient)
        {
            // The cache list is sorted: use binary search then
            _activeMailBoxCacheLock.EnterReadLock();
            var isActive = _activeMailBoxCache.BinarySearch(recipient) > -1;
            _activeMailBoxCacheLock.ExitReadLock();
            return isActive;
        }

        private void IncomingMailDispatcherThread()
        {
            while (true)
            {
                try
                {
                    using (var c = DB.CreateConnection())
                    {
                        IncomingMailDTO m;
                        c.Open();
                        while (_queue.TryTake(out m, 10 * 1000))
                        {
                            DispatchIncomingMail(c, m);
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private void ActiveMailboxUpdaterThread()
        {
            while (true)
            {
                try { UpdateActiveMailBoxCache(); } catch { }
                Thread.Sleep(5 * 1000);
            }
        }

        private void DispatchIncomingMail(IDbConnection conn, IncomingMailDTO m)
        {
            conn.Execute("insert into IncomingMail (ReceivedOn,Recipient,Sender,ContentSize,ContentId) values (@ReceivedOn,@Recipient,@Sender,@ContentSize,@ContentId);", m);
        }

        private void UpdateActiveMailBoxCache()
        {
            List<string> cache = null;
            using (var c = DB.CreateConnection())
            {
                c.Open();
                var query = c.Query<string>("select Address from MailBox where ExpiresOn > @CurrentDateAndTime order by Address;", new { CurrentDateAndTime = DateTime.UtcNow });
                cache = query.AsList();
            }

            _activeMailBoxCacheLock.EnterWriteLock();
            _activeMailBoxCache = cache;
            _activeMailBoxCacheLock.ExitWriteLock();
        }
    }
}
