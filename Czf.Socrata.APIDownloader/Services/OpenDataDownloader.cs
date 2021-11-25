using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

namespace Czf.Socrata.APIDownloader.Services;

public class OpenDataDownloader : BackgroundService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IOptions<OpenDataDownloaderOptions> _options;
    private readonly HttpClient _httpClient;

    public OpenDataDownloader(
        IHostApplicationLifetime applicationLifetime,
        IOptions<OpenDataDownloaderOptions> options,
        HttpClient httpClient)
    {
        _applicationLifetime = applicationLifetime;
        _options = options;
        _httpClient = httpClient;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OpenDataDownloaderOptions options = _options.Value;
        int queryLimitPerPage = GetQueryLimitPerPage(options.DataUri);
        int fileCount = 0;
        int pageCount = 0;
        bool fileCreated;
        string tempBaseFileName = Path.GetTempFileName();
        File.Delete(tempBaseFileName);
        long bytesWritten = 0;


        
        FileStream dataFile = await FileStart(fileCount, tempBaseFileName, stoppingToken);
        try
        {
            do
            {
                fileCreated = false;
                string paginatedQuery = options.DataUri.Query + (options.DataUri.Query.Length > 0 ? "&" : "?") + GetOffset(fileCount, pageCount, options.QueryPagesPerFile, queryLimitPerPage);
                Uri paginatedUri = new Uri(options.DataUri.GetLeftPart(UriPartial.Path) + paginatedQuery);
                using HttpRequestMessage httpRequestMessage = new(
                            HttpMethod.Get,
                            paginatedUri);

                httpRequestMessage.Headers.Add("X-App-Token", options.AppToken);
                HttpResponseMessage httpResponseMessage =
                    await _httpClient.SendAsync(httpRequestMessage, stoppingToken);
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine("unsuccessful response with uri: " + paginatedUri.ToString());
                    break;
                }

                Stream contentStream = httpResponseMessage.Content.ReadAsStream(stoppingToken);
                if (contentStream.Length > 3)
                {
                    await contentStream.CopyToAsync(dataFile, stoppingToken);
                    await dataFile.WriteAsync(new byte[] { (byte)',' }, stoppingToken);
                }
                bytesWritten = contentStream.Length;
                pageCount++;
                if (pageCount >= options.QueryPagesPerFile)
                {
                    await FileEnd(dataFile, stoppingToken);

                    pageCount = 0;
                    fileCreated = true;
                    
                    
                    fileCount++;
                    dataFile = await FileStart(fileCount, tempBaseFileName, stoppingToken);
                }



            } while ((bytesWritten > 3) && !stoppingToken.IsCancellationRequested);
            if (bytesWritten == 3 && fileCreated)
            {
                File.Delete(GenerateTempFileName(fileCount, tempBaseFileName));
            }
            else
            {
                dataFile.Seek(-1, SeekOrigin.Current);
                await FileEnd(dataFile, stoppingToken);
            }
        }
        finally
        {
            if (dataFile != null)
            {
                await dataFile.DisposeAsync();
            }
            _applicationLifetime.StopApplication();
        }
    }

    private static async ValueTask<FileStream> FileStart(int fileCount, string tempBaseFileName, CancellationToken cancellationToken)
    {
        FileStream dataFile = File.Create(GenerateTempFileName(fileCount, tempBaseFileName));
        await dataFile.WriteAsync(new byte[] { (byte)'[' }, cancellationToken);
        return dataFile;
    }

    private static async ValueTask FileEnd(FileStream dataFile, CancellationToken cancellationToken)
    {
        await dataFile.WriteAsync(new byte[] { (byte)']' }, cancellationToken);
        dataFile.Close();
        await dataFile.DisposeAsync();
    }



    private static string GenerateTempFileName(int fileCount, string tempBaseFileName)
    {
        return tempBaseFileName.Replace(".tmp", "_" + DateTime.Now.ToBinary() + "_" + fileCount + ".tmp");
    }

    private static int GetQueryLimitPerPage(Uri dataUri)
    {
        int result = 1000;
        var query = dataUri.Query;
        var queryStringKvp = HttpUtility.ParseQueryString(query);
        var limitValue = queryStringKvp["$limit"];

        if (limitValue != null)
        {
            result = int.Parse(limitValue);
        }

        return result;

    }

    private static string GetOffset(int fileCount, int queryPageCount, int queryPagesPerFile, int limit = 1000)
    {
        int offset = ((limit * (queryPagesPerFile)) * fileCount) + limit * queryPageCount;
        return $"$offset={offset}";
    }


    public class OpenDataDownloaderOptions
    {
        public const string CONFIG_KEY_APP_TOKEN = "socrataAppToken";

        public Uri DataUri { get; set; }
        public string AppToken { get; set; }
        public int QueryPagesPerFile { get; set; } = 10;
    }
}
