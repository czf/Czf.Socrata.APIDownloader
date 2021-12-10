using Czf.Socrata.APIDownloader.Observables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<OpenDataDownloader> _logger;
    private readonly IOptions<OpenDataDownloaderOptions> _options;
    private readonly HttpClient _httpClient;
    private readonly MoveFilesToDestinationContextObservable _moveFilesToDestinationContextObservable;

    public OpenDataDownloader(
        IHostApplicationLifetime applicationLifetime,
        ILogger<OpenDataDownloader> logger,
        IOptions<OpenDataDownloaderOptions> options,
        HttpClient httpClient,
        MoveFilesToDestinationContextObservable moveFilesToDestinationContextObservable)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _options = options;
        _httpClient = httpClient;
        _moveFilesToDestinationContextObservable = moveFilesToDestinationContextObservable;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OpenDataDownloaderOptions options = _options.Value;
        int queryLimitPerPage = GetQueryLimitPerPage(options.DataUri);
        int fileCount = 0;
        int pageCount = 0;
        bool createFile =false;
        string tempBaseFileName = Path.GetTempFileName();
        File.Delete(tempBaseFileName);
        long bytesWritten = 0;
        string appToken = options.SocrataAppToken;
        string fileName;
        if (String.IsNullOrEmpty(options.SocrataAppToken) || string.IsNullOrWhiteSpace(options.SocrataAppToken))
        {
            _logger.LogWarning("Application token not provided.  API throttling limits are higher when using an application token. https://dev.socrata.com/docs/app-tokens.html");
        }
        else
        {
            appToken = options.SocrataAppToken;
        }

        FileStream dataFile;
        (dataFile, fileName) = await FileStart(fileCount, tempBaseFileName, stoppingToken);
        try
        {
            do
            {
                if (createFile)
                {
                    (dataFile, fileName) = await FileStart(fileCount, tempBaseFileName, stoppingToken);
                }
                string paginatedQuery = options.DataUri.Query + (options.DataUri.Query.Length > 0 ? "&" : "?") + GetOffset(fileCount, pageCount, options.QueryPagesPerFile, queryLimitPerPage);
                Uri paginatedUri = new(options.DataUri.GetLeftPart(UriPartial.Path) + paginatedQuery);
                using HttpRequestMessage httpRequestMessage = new(
                            HttpMethod.Get,
                            paginatedUri);

                if (!string.IsNullOrEmpty(appToken))
                {
                    httpRequestMessage.Headers.Add("X-App-Token", options.SocrataAppToken);
                }
                
                HttpResponseMessage httpResponseMessage =
                    await _httpClient.SendAsync(httpRequestMessage, stoppingToken);
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    _logger.LogError("unsuccessful response with uri: " + paginatedUri.ToString());
                    _logger.LogError($"{httpResponseMessage.StatusCode}");
                    _logger.LogError($"{httpResponseMessage.ReasonPhrase}");                    

                    break;
                }

                Stream contentStream = httpResponseMessage.Content.ReadAsStream(stoppingToken);
                if (contentStream.Length > 3)
                {
                    await contentStream.CopyToAsync(dataFile, stoppingToken);
                    
                }
                bytesWritten = contentStream.Length;
                pageCount++;
                if (pageCount >= options.QueryPagesPerFile)
                {
                    Console.WriteLine($"Downloaded file number {fileCount+1} with {options.QueryPagesPerFile} pages per file");
                    await FileEnd(dataFile, stoppingToken);

                    pageCount = 0;
                    createFile = true;
                    
                    
                    fileCount++;

                    
                }
                else
                { 
                    createFile = false; 
                    await dataFile.WriteAsync(new byte[] { (byte)',' }, stoppingToken); 
                }



            } while ((bytesWritten > 3) && !stoppingToken.IsCancellationRequested);
            if (bytesWritten <= 3 && createFile)
            {
                File.Delete(fileName);
                Console.WriteLine($"Completed a total of {fileCount-1} files with content");
            }
            else
            {
                dataFile.Seek(-1, SeekOrigin.Current);
                await FileEnd(dataFile, stoppingToken);
                Console.WriteLine($"Completed a total of {fileCount + 1} files with content");
            }
            string filename = Path.GetFileName(tempBaseFileName);
            string path = tempBaseFileName.Replace(filename, string.Empty);
            _moveFilesToDestinationContextObservable
                .MoveFilesToDestinationFromPattern(path, filename.Replace(".tmp", "_*_*.json"));
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error from Data Downloader");
        }
        finally
        {
            if (dataFile != null)
            {
                await dataFile.DisposeAsync();
            }
            await _moveFilesToDestinationContextObservable.MarkComplete();
        }
    }

    private static async ValueTask<FileStartRecord> FileStart(int fileCount, string tempBaseFileName, CancellationToken cancellationToken)
    {
        string fileName = GenerateTempFileName(fileCount, tempBaseFileName);
        FileStream dataFile = File.Create(fileName);
        await dataFile.WriteAsync(new byte[] { (byte)'[' }, cancellationToken);
        return new FileStartRecord(dataFile, fileName);
    }

    private static async ValueTask FileEnd(FileStream dataFile, CancellationToken cancellationToken)
    {
        await dataFile.WriteAsync(new byte[] { (byte)']' }, cancellationToken);
        dataFile.Close();
        await dataFile.DisposeAsync();
    }



    private static string GenerateTempFileName(int fileCount, string tempBaseFileName)
    {
        return tempBaseFileName.Replace(".tmp", "_" + DateTime.Now.ToBinary() + "_" + fileCount + ".json");
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

    private record class FileStartRecord(FileStream DataFile, string FileName);

    public class OpenDataDownloaderOptions
    {
        public Uri DataUri { get; set; }
        public string SocrataAppToken { get; set; }
        public int QueryPagesPerFile { get; set; } = 10;
    }
}
