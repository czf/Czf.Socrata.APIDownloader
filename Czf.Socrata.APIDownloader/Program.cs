using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Czf.Socrata.APIDownloader.Services;
using OpenDataDownloaderOptions = Czf.Socrata.APIDownloader.Services.OpenDataDownloader.OpenDataDownloaderOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SocrataAPIDownloader;

class Program
{
    static async Task Main(string[] args)
    {
        await CreateHostBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var config = hostContext.Configuration;

                services
                .AddOptions()
                .Configure<OpenDataDownloaderOptions>(x=> 
                {
                    x.AppToken = config.GetValue<string>(OpenDataDownloaderOptions.CONFIG_KEY_APP_TOKEN);
                    x.DataUri = new Uri(config.GetValue<string>(nameof(OpenDataDownloaderOptions.DataUri)));
                })
                .AddHttpClient()
                .AddHostedService<OpenDataDownloader>();
            })
            .RunConsoleAsync();
    }
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
        .ConfigureHostConfiguration(configHost =>
        {
            configHost
            .AddEnvironmentVariables(prefix: "ASPNETCORE_")
            .AddCommandLine(args);
        })
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.Sources.Clear();
            config
            .AddEnvironmentVariables(prefix: "ASPNETCORE_")
            .AddJsonFile("appsettings.json")
            .AddCommandLine(args);
            Console.WriteLine( Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                config.AddUserSecrets<Program>();
            }
        });
}


