using System.Data;
using System.Linq;
using System.IO;
using System.Text;
using Common;
using MimeKit;
using Dapper;
using System;
using System.Globalization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO.Compression;

namespace MailExtractor
{
    public sealed class Extractor
    {
        public const string InlineSubDirectory = "inline";
        public const string AttachmentSubDirectory = "attachments";

        private readonly IDbConnection _conn;
        private readonly IncomingMailDTO _m;

        public Extractor(IDbConnection conn, IncomingMailDTO m)
        {
            _conn = conn;
            _m = m;
        }

        public void Extract()
        {
            var utcnow = DateTime.UtcNow;

            // Obtain the mailbox token of the recipient
            var mbox = _conn.QuerySingleOrDefault<MailboxDTO>("select Id,Token,Address,ExpiresOn from MailBox where Address = @Recipient", _m);
            if (mbox == null)
            {
                Console.WriteLine(string.Format("Could not find mailbox for {0}", _m.Recipient));
                return;
            }

            // Create mail entry
            var mail = new MailDTO() { IdMailBox = mbox.Id, ReceivedOn = _m.ReceivedOn, Size = _m.ContentSize };
            mail.Id = _conn.QuerySingle<int>("insert into Mail (IdMailBox,ReceivedOn,Size) values (@IdMailBox,@ReceivedOn,@Size) returning Id;", mail);

            // Create mailbox directory structure
            var mailboxDir = Path.Combine(Configuration.Instance.MailboxDirectory, mbox.Token.ToString());
            var mailDir = Path.Combine(mailboxDir, mail.Id.ToString(CultureInfo.InvariantCulture));
            var mailInlineDir = Path.Combine(mailDir, InlineSubDirectory);
            var mailAttachmentDir = Path.Combine(mailDir, AttachmentSubDirectory);
            Directory.CreateDirectory(mailboxDir);
            Directory.CreateDirectory(mailDir);
            Directory.CreateDirectory(mailInlineDir);
            Directory.CreateDirectory(mailAttachmentDir);

            var me = new MailEntry();
            var mailFilename = _m.GetContentFileName();
            using (var mailStream = new FileStream(mailFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Parse MIME message 
                var msg = MimeMessage.Load(ParserOptions.Default, mailStream, true);

                me.Id = mail.Id;
                me.ReceivedOn = _m.ReceivedOn;
                me.Sender = _m.Sender;
                me.Subject = msg.Subject;
                me.Size = _m.ContentSize;

                // Generate HTML preview and save inline images
                var preview = new HtmlPreviewVisitor(mailInlineDir, InlineSubDirectory + '/');
                msg.Accept(preview);

                // Save sanitized mail content
                using (var fs = new FileStream(Path.Combine(mailDir, "mail.html"), FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    var html = preview.HtmlBody;

                    // Plug js auto sizer at end of <head>, end of <body> or just append it (sometimes, we only get HTML snippet withou body).
                    var js = "<script src=\"/js/iframeResizer.contentWindow.min.js\" async></script>";
                    int idx = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (idx == -1)
                    {
                        idx = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    }
                    if (idx != -1)
                    {
                        html = html.Insert(idx, js);
                        sw.Write(html);
                    }
                    else
                    {
                        // sometimes all we get is an html snippet
                        sw.Write(js);
                        sw.Write(html);
                    }                    
                }

                // Save attachments
                foreach (var attachment in preview.Attachments)
                {
                    // TODO: cleanup filename
                    var dirtyName = attachment.ContentDisposition.FileName;
                    var cleanName = CleanupFilename(dirtyName);
                    me.Attachments.Add(new MailEntry.Attachment() { OriginalFileName = dirtyName, FileName = cleanName });
                    using (var attachmentStream = new FileStream(Path.Combine(mailAttachmentDir, cleanName), FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        if (attachment is MessagePart)
                        {
                            var part = (MessagePart)attachment;
                            part.Message.WriteTo(attachmentStream);
                        }
                        else
                        {
                            var part = (MimePart)attachment;
                            part.ContentObject.DecodeTo(attachmentStream);
                        }
                    }
                }
            }

            // Load existing mails to insert new mail
            var mails = new List<MailEntry>();
            string jsonFile = Path.Combine(mailboxDir, "mails.json");
            if (File.Exists(jsonFile))
            {
                using (var fs = new FileStream(jsonFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    var content = reader.ReadToEnd();
                    mails = JsonConvert.DeserializeObject<List<MailEntry>>(content);
                }
            }
            mails.Add(me);

            // mail.json & mail.json.gz
            var json = JsonConvert.SerializeObject(mails);
            using (var fs = new FileStream(jsonFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, Encoding.UTF8))
            {
                writer.Write(json);
            }
            Touch(jsonFile, utcnow);

            using (var fs = new FileStream(jsonFile + ".gz", FileMode.Create, FileAccess.Write, FileShare.None))
            using (var gzip = new GZipStream(fs, CompressionLevel.Fastest))
            using (var writer = new StreamWriter(gzip, Encoding.UTF8))
            {
                writer.Write(json);
            }
            Touch(jsonFile + ".gz", utcnow);

            Gzipify(mailDir, utcnow);
        }

        /// <summary>
        /// GZIP all files recursively inside a given folder. This is use by NGINX gzip_static module
        /// to obtain precompressed files and reduce CPU usage of the web server: it won't do dynamic
        /// compression all the time for clients requesting gzipped content.
        /// </summary>
        /// <param name="mailDir"></param>
        private void Gzipify(string mailDir, DateTime timestamp)
        {
            var includedExtensions = new[] { ".json", ".html", ".txt", ".doc", ".pdf", ".xls", ".bmp" };
            try
            {
                var mailFiles = Directory.GetFiles(mailDir, "*", SearchOption.AllDirectories);
                foreach (var inputFile in mailFiles)
                {
                    if (!includedExtensions.Any((ext) => inputFile.ToLowerInvariant().EndsWith(ext))) {
                        continue;
                    }

                    using (var input = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(inputFile + ".gz", FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var gzipOutput = new GZipStream(output, CompressionLevel.Fastest))
                        input.CopyTo(gzipOutput);

                    // nginx gzip_static recommends both files having the same modification timestamp
                    Touch(inputFile, timestamp);
                    Touch(inputFile + ".gz", timestamp);
                }
            }
            catch
            {
            }
        }

        private void Touch(string filename, DateTime utcTimeStamp)
        {
            File.SetLastWriteTimeUtc(filename, utcTimeStamp);
        }

        private string CleanupFilename(string filename)
        {
            var clean = filename;
            foreach (var ch in Path.GetInvalidFileNameChars())
                clean = clean.Replace(ch, '_');

            return clean;
        }
    }
}
