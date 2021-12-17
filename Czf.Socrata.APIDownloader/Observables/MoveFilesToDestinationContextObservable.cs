using Czf.Socrata.APIDownloader.Domain;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Czf.Socrata.APIDownloader.Observables;
public class MoveFilesToDestinationContextObservable : IObservable<MoveFilesToDestinationContext>, IDisposable
{
    readonly List<IObserver<MoveFilesToDestinationContext>> _observers;
    
    private readonly Task _processTask;
    private readonly Task _completeTask;
    private readonly ConcurrentQueue<MoveFilesToDestinationContext> _contextQueue;
    
    private readonly AutoResetEvent _processEvent;
    private readonly SemaphoreSlim _completeSemaphore;
    bool _keepRunning;
    private bool disposedValue;
    private bool _complete;

    public MoveFilesToDestinationContextObservable(IHostApplicationLifetime hostApplicationLifetime)
    {
        _processEvent = new AutoResetEvent(false);
        _completeSemaphore = new SemaphoreSlim(0,1);
        _observers = new List<IObserver<MoveFilesToDestinationContext>>();
        _keepRunning = true;
        _contextQueue = new ConcurrentQueue<MoveFilesToDestinationContext>();
        var stopping = () =>
        {
            _keepRunning = false;
            Complete();
            _processEvent.Set();
        };
        hostApplicationLifetime.ApplicationStopped.Register(stopping);
        hostApplicationLifetime.ApplicationStopping.Register(stopping);

        _processTask = Task.Run(() => ProcessPattern());
        _completeTask = Task.Run(async () => {
            await _completeSemaphore.WaitAsync();
            while (!_contextQueue.IsEmpty)
            {
                await Task.Delay(1000);
            }
            Complete();
        });
        
    }

    public IDisposable Subscribe(IObserver<MoveFilesToDestinationContext> observer)
    {
        if (_complete)
        {
            throw new InvalidOperationException("Observable is completed");
        }
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
        return new Unsubscriber<MoveFilesToDestinationContext>(_observers, observer);
    }
    public void MoveFilesToDestinationFromPattern(string sourcePath, string fileNamePattern)
    {
        if (_complete)
        {
            throw new InvalidOperationException("Observable is completed");
        }
        MoveFilesToDestinationContext context = new(sourcePath, fileNamePattern);
        _contextQueue.Enqueue(context);
        _processEvent.Set();
    }

    private void ProcessPattern()
    {
        while (_keepRunning)
        {
            _processEvent.WaitOne(); 
            if (_contextQueue.TryDequeue(out var context))
            {
                foreach (var observer in _observers)
                {
                    observer.OnNext(context);
                }
            }
        }
    }
    public async Task MarkComplete()
    {
        _complete = true;
        while (!_contextQueue.IsEmpty) { await Task.Delay(1000); }
        _keepRunning = false;
        _completeSemaphore.Release();
    }

    private void Complete()
    {
        foreach (var observer in _observers)
        {
            observer.OnCompleted();
        }
        _observers.Clear();
    }
    

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _processEvent.Dispose();
                _completeSemaphore.Dispose();
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
