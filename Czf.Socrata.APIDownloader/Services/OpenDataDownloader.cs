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
using static Czf.Socrata.APIDownloader.Services.OpenDataDownloader;

namespace Czf.Socrata.APIDownloader.Services;

public class OpenDataDownloader : BackgroundService, IObservable<FileDownloadedContext>, IDisposable
{
    readonly List<IObserver<FileDownloadedContext>> _observers;

    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<OpenDataDownloader> _logger;
    private readonly IOptions<OpenDataDownloaderOptions> _options;
    private readonly HttpClient _httpClient;

    private bool _complete;

    public OpenDataDownloader(
        IHostApplicationLifetime applicationLifetime,
        ILogger<OpenDataDownloader> logger,
        IOptions<OpenDataDownloaderOptions> options,
        HttpClient httpClient)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _options = options;
        _httpClient = httpClient;
        _observers = new List<IObserver<FileDownloadedContext>>();

    }

    public IDisposable Subscribe(IObserver<FileDownloadedContext> observer)
    {
        if (_complete)
        {
            throw new InvalidOperationException("Observable is completed");
        }
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
        return new Unsubscriber<FileDownloadedContext>(_observers, observer);

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OpenDataDownloaderOptions options = _options.Value;
        int queryLimitPerPage = GetQueryLimitPerPage(options.DataUri);
        int fileCount = 0;
        int pageCount = 0;
        bool createFile = false;
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
            if (!options.SkipDownload)
            {
                do
                {
                    if (createFile)
                    {
                        (dataFile, fileName) = await FileStart(fileCount, tempBaseFileName, stoppingToken);
                    }
                    long offsetValue = GetOffsetValue(fileCount, pageCount, options.QueryPagesPerFile, queryLimitPerPage);
                    string paginatedQuery = options.DataUri.Query + (options.DataUri.Query.Length > 0 ? "&" : "?") + FormatOffset(offsetValue);
                    Uri paginatedUri = new(options.DataUri.GetLeftPart(UriPartial.Path) + paginatedQuery);

                    Console.WriteLine($"fetch page: {paginatedUri}");

                    using HttpRequestMessage httpRequestMessage = new(
                                HttpMethod.Get,
                                paginatedUri);

                    if (!string.IsNullOrEmpty(appToken))
                    {
                        httpRequestMessage.Headers.Add("X-App-Token", options.SocrataAppToken);
                    }

                    await WaitForBasicThrottleAsync();
                    using HttpResponseMessage httpResponseMessage = await FetchPage(paginatedUri, httpRequestMessage, stoppingToken);

                    using Stream contentStream = httpResponseMessage.Content.ReadAsStream(stoppingToken);
                    if (contentStream.Length > 3)
                    {
                        await contentStream.CopyToAsync(dataFile, stoppingToken);

                    }
                    bytesWritten = contentStream.Length;
                    pageCount++;
                    if (pageCount >= options.QueryPagesPerFile)
                    {
                        Console.WriteLine($"Downloaded file number {fileCount + 1} with {options.QueryPagesPerFile} pages per file");
                        await FileEnd(dataFile, stoppingToken);

                        NotifyObserversFiledownloaded(fileName, bytesWritten, offsetValue);
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
                if (bytesWritten <= 3)
                {
                    if (!createFile)
                    {
                        dataFile.Close();
                    }
                    File.Delete(fileName);
                    Console.WriteLine($"Completed a total of {fileCount-1} files with content");
                }
                else
                {
                    dataFile.Seek(-1, SeekOrigin.Current);
                    await FileEnd(dataFile, stoppingToken);
                    Console.WriteLine($"Completed a total of {fileCount} files with content");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error from Data Downloader");
        }
        finally
        {
            if (dataFile != null)
            {
                await dataFile.DisposeAsync();
            }
            NotifyObserversFiledownloadsComplete();
        }

        
    }

    private async Task<HttpResponseMessage> FetchPage(Uri paginatedUri, HttpRequestMessage httpRequestMessage, CancellationToken stoppingToken)
    {
        HttpResponseMessage httpResponseMessage;
        do
        {
            httpResponseMessage =
                await _httpClient.SendAsync(httpRequestMessage, stoppingToken);
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                _logger.LogError("unsuccessful response with uri: " + paginatedUri.ToString());
                _logger.LogError($"StatusCode: {httpResponseMessage.StatusCode}");
                _logger.LogError($"ReasonPhrase: {httpResponseMessage.ReasonPhrase}");
                _logger.LogError($"Content: {await httpRequestMessage.Content?.ReadAsStringAsync()}");
                await Task.Delay(1000);
            }

        } while (!httpResponseMessage.IsSuccessStatusCode);
        return httpResponseMessage;
    }

    private void NotifyObserversFiledownloadsComplete()
    {
        foreach (var observer in _observers)
        {
            observer.OnCompleted();
        }
        _complete = true;
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

    /// <summary>
    /// Ran into an issue where requests would return wrong data for the specified offset.  Could
    /// be related to requests occuring too quickly.
    /// </summary>
    private static async Task WaitForBasicThrottleAsync()
    {
        SemaphoreSlim throttleSemaphore = new SemaphoreSlim(0);
        System.Timers.Timer throttle = new(1000) { AutoReset = false };
        throttle.Elapsed += (x, y) => { throttleSemaphore.Release(); };
        throttle.Start();
        await throttleSemaphore.WaitAsync();
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

    private long GetOffsetValue(int fileCount, int queryPageCount, int queryPagesPerFile, int limit = 1000)
        => ((limit * (queryPagesPerFile)) * fileCount) + limit * queryPageCount;
    private static string FormatOffset(long offsetValue)
        => $"$offset={offsetValue}";
    

    private record class FileStartRecord(FileStream DataFile, string FileName);

    private void NotifyObserversFiledownloaded(string fileName, long bytesWritten, long offset)
    {
        if(bytesWritten <= 3) { return; }
        foreach (var observer in _observers)
        {
            observer.OnNext(new(fileName, offset));
        }
    }

    public class OpenDataDownloaderOptions
    {
        public Uri DataUri { get; set; }
        public string SocrataAppToken { get; set; }
        public int QueryPagesPerFile { get; set; } = 10;

        public bool SkipDownload { get; set; } = false;
    }

    public record class FileDownloadedContext(string FileName, long Offset);
}
