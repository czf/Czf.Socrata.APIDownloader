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
    private readonly UTF8Encoding _encoding;

    public OpenDataDownloader(
        IHostApplicationLifetime applicationLifetime,
        IOptions<OpenDataDownloaderOptions> options,
        HttpClient httpClient)
    {
        _applicationLifetime = applicationLifetime;
        _options = options;
        _httpClient = httpClient;
        _encoding = new UTF8Encoding(true);
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
        //********************************************************


        //need second loop to continue in existing file with outer loop being used to create new file


        //**********************************************************

        FileStream dataFile = File.Create(GenerateTempFileName(fileCount, tempBaseFileName));
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
                }
                bytesWritten = contentStream.Length;
                pageCount++;
                if (pageCount >= options.QueryPagesPerFile)
                {
                    pageCount = 0;
                    fileCreated = true;
                    dataFile.Close();
                    await dataFile.DisposeAsync();
                    fileCount++;
                    dataFile = File.Create(GenerateTempFileName(fileCount, tempBaseFileName));
                }



            } while ((bytesWritten > 3) && !stoppingToken.IsCancellationRequested);
            if (bytesWritten == 3 && fileCreated)
            {
                File.Delete(GenerateTempFileName(fileCount, tempBaseFileName));
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

    private static string GenerateTempFileName(int fileCount, string tempBaseFileName)
    {
        return tempBaseFileName.Replace(".tmp", "_" + fileCount + ".tmp");
    }

    protected async Task ExecuteAsync_OLD(CancellationToken stoppingToken)
    {
        OpenDataDownloaderOptions options = _options.Value;
        HttpRequestMessage httpRequestMessage = null;
        HttpResponseMessage httpResponseMessage = null;
        FileStream tempFileStream = null;
        try
        {
            int queryLimitPerPage = GetQueryLimitPerPage(options.DataUri);
            httpRequestMessage = new(
                HttpMethod.Get,
                options.DataUri);
            httpRequestMessage.Headers.Add("X-App-Token", options.AppToken);
            httpResponseMessage =
                await _httpClient.SendAsync(httpRequestMessage, stoppingToken);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                string tempFileName = Path.GetTempFileName();
                tempFileStream = File.Create(tempFileName);
                string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(stoppingToken);
                //responseContent = responseContent[1..^1];
                int fileCount = 0;
                int pageCount = 0;
                var responseList = JsonSerializer.Deserialize<List<object>>(responseContent);
                while (responseContent.Length > 0 && responseList.Any() && httpResponseMessage.IsSuccessStatusCode && !stoppingToken.IsCancellationRequested)
                {
                    byte[] content = _encoding.GetBytes(responseContent);
                    await tempFileStream.WriteAsync(content, stoppingToken);
                    if (pageCount >= options.QueryPagesPerFile - 1)
                    {
                        fileCount++;
                        pageCount = 0;
                        await tempFileStream.DisposeAsync();
                        tempFileStream = File.Create($"{tempFileName}_{fileCount}");
                    }
                    pageCount++;
                    string paginatedQuery = options.DataUri.Query + (options.DataUri.Query.Length > 0 ? "&" : "?") + GetOffset(fileCount, pageCount, options.QueryPagesPerFile, queryLimitPerPage);
                    Uri paginatedUri = new Uri(options.DataUri.GetLeftPart(UriPartial.Path) + paginatedQuery);


                    httpRequestMessage = new(
                        HttpMethod.Get,
                        paginatedUri);
                    httpRequestMessage.Headers.Add("X-App-Token", options.AppToken);
                    httpResponseMessage =
                        await _httpClient.SendAsync(httpRequestMessage, stoppingToken);
                    responseContent = await httpResponseMessage.Content.ReadAsStringAsync(stoppingToken);
                    responseList = JsonSerializer.Deserialize<List<object>>(responseContent);
                }
            }
        }
        catch (Exception e)
        {
            throw;
        }
        finally
        {
            httpRequestMessage?.Dispose();
            httpResponseMessage?.Dispose();
            if (tempFileStream != null)
            {
                await tempFileStream.DisposeAsync();
            }
            _applicationLifetime.StopApplication();
        }

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

    private string GetOffset(int fileCount, int queryPageCount, int queryPagesPerFile, int limit = 1000)
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
