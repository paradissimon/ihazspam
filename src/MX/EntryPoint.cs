using System;
using Common;

namespace MX
{
    public class EntryPoint
    {
        public static void Main(string[] args)
        {
            try
            {
                var loadConfig = Configuration.Instance;
                new SmtpServer().Run();
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
