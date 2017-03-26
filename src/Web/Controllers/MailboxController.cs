using System;
using System.Threading.Tasks;
using Common;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class MailboxController : ControllerBase
    {
        [HttpGet]
        public async Task<object> Ping()
        {
            using (var c = DB.CreateConnection())
            {
                await c.OpenAsync();
                return new { Status = "alive", DateTimeUtc = DateTime.UtcNow, Postgresql = c.ServerVersion };
            }
        }

        [HttpPost]
        public async Task<object> CreateMailAccount()
        {
            MailboxDTO mbox;
            using (var c = DB.CreateConnection())
            {
                await c.OpenAsync();
                mbox = MailboxDTO.CreateRandomOne();
                mbox.Id = await c.QuerySingleAsync<int>("insert into Mailbox (Token, Address, ExpiresOn) values (@Token, @Address, @ExpiresOn) returning Id;", mbox);                                
            }
            mbox.Id = 0; // hide this
            return mbox;
        }

        [HttpGet]
        public async Task<object> GetMailAccount(Guid m)
        {
            MailboxDTO mbox;
            using (var c = DB.CreateConnection())
            {
                await c.OpenAsync();
                mbox = c.QuerySingleOrDefault<MailboxDTO>("select Id,Token,Address,ExpiresOn from MailBox where Token = @Token", new { Token = m, UtcNow = DateTime.UtcNow });
            }
            mbox.Id = 0; // hide this
            return mbox;
        }
    }
}
