using Czf.Socrata.APIDownloader.Domain;
using Czf.Socrata.APIDownloader.Observables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Czf.Socrata.APIDownloader.Services.OpenDataDownloader;

namespace Czf.Socrata.APIDownloader.Services;
public class MoveFileToDestination : IHostedService, IDisposable
{
    private readonly IObservable<FileDownloadedContext> _observable;
    private readonly SQLImportObservable _sqlImportObservable;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IOptions<MoveFileToDestinationOptions> _options;
    private readonly ILogger<MoveFileToDestination> _logger;
    private bool disposedValue;
    private IDisposable _subscription;

    public MoveFileToDestination(
        IHostApplicationLifetime applicationLifetime,
        IOptions<MoveFileToDestinationOptions> options,
        IObservable<FileDownloadedContext> observable,
        ILogger<MoveFileToDestination> logger,
        SQLImportObservable sqlImportObservable)
    {
        _applicationLifetime = applicationLifetime;
        _options = options;
        if (!Directory.Exists(_options.Value.FileTargetDestination)){
            throw new ArgumentException("FileTargetDestination path does not exist.");
        }

        _observable = observable;
        _sqlImportObservable = sqlImportObservable;
        _logger = logger;
        
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _observable.Subscribe(new ContextObserver(_options.Value, _logger, _applicationLifetime, _sqlImportObservable));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription.Dispose();
        return Task.CompletedTask;
    }

    public class MoveFileToDestinationOptions
    {
        public string FileTargetDestination { get; set; } = ".";
        public string FileTargetBaseName { get; set; } = "result.json";
        public bool ImportToDatabaseEnabled { get; set; } = true;
        public bool SkipDownload { get; set; } = false;

    }
    public class FileMovedContext
    {

    }
    private class ContextObserver : IObserver<FileDownloadedContext>//, IObservable<FileMovedContext>
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly MoveFileToDestinationOptions _options;
        private readonly ILogger _logger;
        private readonly string _extension;
        private readonly SemaphoreSlim _completeSemaphore;
        private readonly SemaphoreSlim _processEvent;
        private bool _keepRunning;

        private readonly SQLImportObservable _sqlImportObservable;
        private readonly ConcurrentQueue<FileDownloadedContext> _contextQueue;

        public ContextObserver(
            MoveFileToDestinationOptions options, 
            ILogger logger, 
            IHostApplicationLifetime applicationLifetime,
            SQLImportObservable sqlImportObservable)
        {
            _processEvent = new SemaphoreSlim(0);
            _keepRunning = true;

            _applicationLifetime = applicationLifetime;
            _options = options;
            _logger = logger;
            _extension = Path.GetExtension(_options.FileTargetBaseName);
            _completeSemaphore = new(1);
            _sqlImportObservable = sqlImportObservable;
            _contextQueue = new ConcurrentQueue<FileDownloadedContext>();
            Task.Run(RunContextQueue);
        }

        public void OnCompleted()
        {
            _completeSemaphore.Wait();
            _completeSemaphore.Release();
            _keepRunning = false;
            _processEvent.Release();
            _logger.LogInformation("Completed: Move Files");
            Console.WriteLine("Completed: Move Files");
            
            if (_options.SkipDownload)
            {
                string fileNamePattern = _options.FileTargetBaseName.Replace(_extension, $"*{_extension}");
                var files = Directory.EnumerateFiles(_options.FileTargetDestination, fileNamePattern);
                foreach (var file in files)
                {
                    _sqlImportObservable.ImportSqlFromJson(file);
                }
            }
            _sqlImportObservable.MarkComplete().Wait();
            if (!_options.ImportToDatabaseEnabled)
            {
                _applicationLifetime.StopApplication();
            }
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(FileDownloadedContext context)
        {
            _contextQueue.Enqueue(context);
            _processEvent.Release();
        }

        private void RunContextQueue()
        {
            while (_keepRunning)
            {
                _processEvent.Wait();
                _completeSemaphore.Wait();
                while (!_contextQueue.IsEmpty)
                {
                    if(_contextQueue.TryPeek(out var context))
                    {
                        ProcessOnNext(context);
                        _contextQueue.TryDequeue(out _);
                    }
                }
                _completeSemaphore.Release();
            }
        }

        private void ProcessOnNext(FileDownloadedContext context)
        {
            Console.WriteLine($"Moving file: {context.FileName}");
            
            try
            {
                var fileName = _options.FileTargetBaseName.Replace(_extension, $"_{context.Offset}{_extension}");
                File.Move(context.FileName, _options.FileTargetDestination + fileName);
                _logger.LogInformation("moved");
                _sqlImportObservable.ImportSqlFromJson(_options.FileTargetDestination + fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"unable to move");
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
}
