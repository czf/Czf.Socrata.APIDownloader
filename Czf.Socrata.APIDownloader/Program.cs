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
using MoveFilesToDestinationOptions = Czf.Socrata.APIDownloader.Services.MoveFilesToDestination.MoveFilesToDestinationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Czf.Socrata.APIDownloader.Observables;
using Czf.Socrata.APIDownloader.Domain;
using static Czf.Socrata.APIDownloader.Observables.SQLImportObservable;

namespace SocrataAPIDownloader;

class Program
{
    static async Task Main(string[] args)
    {
        await CreateHostBuilder(args)
            .ConfigureLogging((context, builder) =>
            {
                builder
                .ClearProviders()
                .AddConsole()
                .SetMinimumLevel(LogLevel.Warning)
                .AddConfiguration(context.Configuration);
                
            })
            .ConfigureServices((hostContext, services) =>
            {
                var config = hostContext.Configuration;

                _ = services
                .AddOptions()
                .Configure<OpenDataDownloaderOptions>(config)
                .Configure<MoveFilesToDestinationOptions>(config)
                .Configure<ImportJsonFromFileContextOptions>(config)

                .AddHostedService<OpenDataDownloader>()
                .AddHostedService<MoveFilesToDestination>()
                .AddHostedService<JsonFileToSqlImporter>()

                .AddSingleton<MoveFilesToDestinationContextObservable>()
                .AddSingleton<IObservable<MoveFilesToDestinationContext>, MoveFilesToDestinationContextObservable>(x =>
                {
                    return x.GetService<MoveFilesToDestinationContextObservable>();
                })
                .AddSingleton<SQLImportObservable>()
                .AddSingleton<IObservable<ImportJsonFromFileContext>, SQLImportObservable>(x =>
                {
                    return x.GetService<SQLImportObservable>();
                })
                .AddHttpClient(string.Empty, (x) => { x.Timeout = Timeout.InfiniteTimeSpan; });
                
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


