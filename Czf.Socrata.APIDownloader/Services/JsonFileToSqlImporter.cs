using Czf.Socrata.APIDownloader.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Czf.Socrata.APIDownloader.Observables.SQLImportObservable;

namespace Czf.Socrata.APIDownloader.Services;

public class JsonFileToSqlImporter : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IOptions<ImportJsonFromFileContextOptions> _options;
    private readonly IObservable<ImportJsonFromFileContext> _observable;
    private readonly ILogger<JsonFileToSqlImporter> _logger;
    private IDisposable _subscription;
    private bool disposedValue;

    public JsonFileToSqlImporter(
        IHostApplicationLifetime applicationLifetime,
        IOptions<ImportJsonFromFileContextOptions> options,
        ILogger<JsonFileToSqlImporter> logger,
        IObservable<ImportJsonFromFileContext> observable)
    {
        _applicationLifetime = applicationLifetime;
        _options = options;
        _observable = observable;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _observable.Subscribe(new ContextObserver(_options.Value, _logger, _applicationLifetime));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription.Dispose();
        return Task.CompletedTask;
    }

    private class ContextObserver : IObserver<ImportJsonFromFileContext>
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ImportJsonFromFileContextOptions _options;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _completeSemaphore;


        public ContextObserver(
            ImportJsonFromFileContextOptions options,
            ILogger logger,
            IHostApplicationLifetime applicationLifetime
            )
        {
            _applicationLifetime = applicationLifetime;
            _options = options;
            _logger = logger;
            _completeSemaphore = new(1);
        }

        public void OnCompleted()
        {
            _completeSemaphore.Wait();
            _completeSemaphore.Release();
            _logger.LogInformation("Completed: Import Json");
            Console.WriteLine("Completed: Import Json");
            _applicationLifetime.StopApplication();

        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(ImportJsonFromFileContext value)
        {
            try
            {
                if (!_options.ImportToDatabaseEnabled) { return; }
                _completeSemaphore.Wait();
                Console.WriteLine($"Import file: {value.filePath}");
                using SqlConnection sqlConnection = new SqlConnection(_options.ConnectionString);
                sqlConnection.Open();
                using SqlCommand sqlCommand = sqlConnection.CreateCommand();
                sqlCommand.CommandText = _options.StoredProcedureProcessJson;
                sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
                SqlParameter sqlParameter = new SqlParameter("FilePath", System.Data.SqlDbType.VarChar);
                sqlParameter.Value = value.filePath;
                sqlCommand.Parameters.Add(sqlParameter);
                sqlCommand.CommandTimeout = 0;
                sqlCommand.ExecuteNonQueryAsync(_applicationLifetime.ApplicationStopped).Wait();
                _completeSemaphore.Release();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "error import json to sql");
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _subscription?.Dispose();
            }            
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public class ImportJsonFromFileContextOptions
    {
        public string ConnectionString { get; set; } = "Server=.;Database=OpenData;Trusted_Connection=True;";
        public string StoredProcedureProcessJson { get; set; } = "usp_ProcessJson";
        public bool ImportToDatabaseEnabled { get; set; } = true;

    }
}
