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
using MoveFileToDestinationOptions = Czf.Socrata.APIDownloader.Services.MoveFileToDestination.MoveFileToDestinationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Czf.Socrata.APIDownloader.Observables;
using Czf.Socrata.APIDownloader.Domain;
using static Czf.Socrata.APIDownloader.Services.JsonFileToSqlImporter;
using static Czf.Socrata.APIDownloader.Services.OpenDataDownloader;

namespace SocrataAPIDownloader;

class Program
{
    static async Task Main(string[] args)
    {
        bool shouldRunHostBuilder = false;
        var host = CreateHostBuilder(args)
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
               shouldRunHostBuilder = CheckStartupConfiguration(args);
               _ = services
               .AddOptions()
               .Configure<OpenDataDownloaderOptions>(config)
               .Configure<MoveFileToDestinationOptions>(config)
               .Configure<ImportJsonFromFileContextOptions>(config)

               .AddSingleton<OpenDataDownloader>()
               .AddHostedService(x => x.GetRequiredService<OpenDataDownloader>())
               .AddHostedService<MoveFileToDestination>()
               .AddHostedService<JsonFileToSqlImporter>()

               .AddSingleton<MoveFilesToDestinationContextObservable>()
               .AddSingleton<IObservable<FileDownloadedContext>, OpenDataDownloader>(x => x.GetRequiredService<OpenDataDownloader>())
               .AddSingleton<SQLImportObservable>()
               .AddSingleton<IObservable<ImportJsonFromFileContext>, SQLImportObservable>(x =>
               {
                   return x.GetService<SQLImportObservable>();
               })
               .AddHttpClient(string.Empty, (x) => { x.Timeout = Timeout.InfiniteTimeSpan; });

           }).Build();

        if (shouldRunHostBuilder)
        {
            await host.RunAsync();
        }
    }

    private static bool CheckStartupConfiguration(string[] args)
    {
        if (args
            .Select(x => 
                x.Replace("/",string.Empty)
                .Replace("-",string.Empty)
                .ToLowerInvariant())
            .Intersect(new string[] {"help","h" } )
            .Any())
        {
            OutputHelpText();
            return false;
        }
        return true;

    }
    private static readonly Dictionary<string, string> _switchMappings = new()
    {
        { "-sproc", "StoredProcedureProcessJson" }
    };
    private static void OutputHelpText()
    {
        Console.WriteLine(
@"
Usage: downsoda [config arguments]

Download all rows from a Socrata hosted Open Data source using a specified resource url.  

All configuration arguments can be specified in Environment Variable, appsettings.json config, 
and/or via command line.  They are read in order and overwrite values from the previous config 
location.


Config Arguments:
  -SocrataAppToken <APPLICATION_TOKEN>                         The token to identify the application with Socrata API
  -SocrataSecretToken <APPLICATION_SECRET_TOKEN>               The token to authenticate the application 
                                                               with Socrata API
  -DataUri <SOCRATA_JSON_URL>                                  The uri of the json data source.
  -FileTargetDestination <PATH_TO_STORE_FILE>                  The location to download the data file. 
                                                               (default: current working directory)
  -FileTargetBaseName <FILENAME>                               The file name that each data file will use as a 
                                                               base template. (default: result.json)
  -SkipDownload <BOOL>                                         When true the DataUri is not downloaded. (default: false)
  -QueryPagesPerFile <NUMBER_OF_PAGES>                         The number of request results to store in one file. 
                                                               (default: 10)
  -ConnectionString <DATABASE_CONNECTIONSTRING>                The connection string to use for importing to 
                                                               MSSQL Server Database.
                                                               (default: Server=.;Database=OpenData;Trusted_Connection=True;)
  -StoredProcedureProcessJson, -sproc <STORED_PROCEDURE_NAME>  The stored procedure to execute expecting the path 
                                                               to one of the data files. (default: usp_ProcessJson)
  -Logging <CONFIGURATION_SETTINGS>                            Logging configuration see: 
                                                               https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-6.0#configure-logging
  -ImportToDatabaseEnabled <BOOL>                              When true the stored procedure specified by 
                                                               StoredProcedureProcessJson will be executed 
                                                               for each data file.  The stored procedure is expected 
                                                               to have a parameter @FilePath, representing the 
                                                               location of the data file. (default: true)
");
    }
    
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
        .ConfigureHostConfiguration(configHost =>
        {
            configHost
            .AddEnvironmentVariables(prefix: "ASPNETCORE_")
            .AddCommandLine(args, _switchMappings);
        })
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.Sources.Clear();
            config
            .AddEnvironmentVariables(prefix: "ASPNETCORE_")
            .AddJsonFile("appsettings.json")
            .AddCommandLine(args, _switchMappings);

            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                config.AddUserSecrets<Program>();
            }
        });
}


