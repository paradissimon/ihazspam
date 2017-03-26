using System;
using System.IO;
using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Web
{
    public class Startup
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel((c) => { c.AddServerHeader = false; })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvcCore().AddJsonFormatters((j) => {
                j.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                j.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                j.Formatting = Formatting.Indented;
                j.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(); // prevent camelCase-ification of JSON output
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseMvc((routes) => {
                routes.MapRoute("API", "api/{controller}/{action}");
            });

            // In development, nginx frontend is not there, so serve static files ourselves...
            if (env.IsDevelopment())
            {
                app.UseStaticFiles(new StaticFileOptions() {
                    OnPrepareResponse = (c) => {
                        var headers = c.Context.Response.GetTypedHeaders();
                        headers.CacheControl = new CacheControlHeaderValue() { MaxAge = TimeSpan.FromSeconds(0), MustRevalidate = true, NoCache = true };
                    }
                });

                app.UseStaticFiles(new StaticFileOptions() {
                    FileProvider = new PhysicalFileProvider(Configuration.Instance.MailboxDirectory),
                    ContentTypeProvider = new FileExtensionContentTypeProvider(),
                    ServeUnknownFileTypes = true,
                    RequestPath = "/m",
                    OnPrepareResponse = (c) => {
                        var headers = c.Context.Response.GetTypedHeaders();
                        headers.CacheControl = new CacheControlHeaderValue() { MaxAge = TimeSpan.FromSeconds(0), MustRevalidate = true, NoCache = true };

                        var isAttachmentDownload = c.Context.Request.Path.Value.ToLowerInvariant().Contains("/attachments/");
                        if (isAttachmentDownload)
                        {
                            headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                        }
                    }
                });
            }
        }
    }
}
