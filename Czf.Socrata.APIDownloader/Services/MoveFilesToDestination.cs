﻿using Czf.Socrata.APIDownloader.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Czf.Socrata.APIDownloader.Services;
public class MoveFilesToDestination : IHostedService, IDisposable
{
    private readonly IObservable<MoveFilesToDestinationContext> _observable;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IOptions<MoveFilesToDestinationOptions> _options;
    private readonly ILogger<MoveFilesToDestination> _logger;
    private bool disposedValue;
    private IDisposable _subscription;

    public MoveFilesToDestination(
        IHostApplicationLifetime applicationLifetime,
        IOptions<MoveFilesToDestinationOptions> options,
        IObservable<MoveFilesToDestinationContext> observable,
        ILogger<MoveFilesToDestination> logger)
    {
        _applicationLifetime = applicationLifetime;
        _options = options;
        if (!Directory.Exists(_options.Value.FileTargetDestination)){
            throw new ArgumentException("FileTargetDestination path does not exist.");
        }

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

    public class MoveFilesToDestinationOptions
    {
        public string FileTargetDestination { get; set; } = ".";
        public string FileTargetBaseName { get; set; } = "result.json";
    }

    private class ContextObserver : IObserver<MoveFilesToDestinationContext>
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly MoveFilesToDestinationOptions _options;
        private readonly ILogger _logger;
        private readonly string _extension;
        private readonly SemaphoreSlim _completeSemaphore;

        public ContextObserver(MoveFilesToDestinationOptions options, ILogger logger, IHostApplicationLifetime applicationLifetime)
        {
            _applicationLifetime = applicationLifetime;
            _options = options;
            _logger = logger;
            _extension = Path.GetExtension(_options.FileTargetBaseName);
            _completeSemaphore = new(1);
        }

        public void OnCompleted()
        {
            _completeSemaphore.Wait();
            _completeSemaphore.Release();
            _logger.LogInformation("Completed");
            Console.WriteLine("Completed");
            _applicationLifetime.StopApplication();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(MoveFilesToDestinationContext context)
        {
            _completeSemaphore.Wait();
            Console.WriteLine("Moving files");
            var files = Directory.EnumerateFiles(context.sourcePath, context.fileNamePattern);
            var currentIndex = 0;
            var numOfFiles = files.Count();
            var maxLeadingZeros = (int)Math.Log10(numOfFiles);
            
            
            foreach (var file in files)
            {
                try
                {
                    Thread.Sleep(1000);
                    int leadingZeros = maxLeadingZeros - Math.Max((int)(Math.Log10(currentIndex)),0);
                    var fileName = _options.FileTargetBaseName.Replace(_extension, $"_{"".PadLeft(leadingZeros, '0')}{currentIndex}{_extension}");
                    File.Move(file, _options.FileTargetDestination + fileName);
                    _logger.LogInformation("moved");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,"unable to move");
                }
                currentIndex++;
            }
            _completeSemaphore.Release();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _subscription.Dispose();
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