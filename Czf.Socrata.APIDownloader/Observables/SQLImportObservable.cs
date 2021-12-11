using Czf.Socrata.APIDownloader.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Czf.Socrata.APIDownloader.Observables;
public class SQLImportObservable : IObservable<ImportJsonFromFileContext>, IDisposable
{
    private bool disposedValue;
    private readonly List<IObserver<ImportJsonFromFileContext>> _observers;
    private readonly SemaphoreSlim _importEvent;
    bool _keepRunning;
    private readonly ConcurrentQueue<ImportJsonFromFileContext> _contextQueue;
    private readonly SemaphoreSlim _completeSemaphore;
    private bool _complete;


    public SQLImportObservable(IHostApplicationLifetime hostApplicationLifetime)
    {
        _importEvent = new SemaphoreSlim(0);
        _completeSemaphore = new SemaphoreSlim(0, 1);
        _observers = new List<IObserver<ImportJsonFromFileContext>>();
        _keepRunning = true;
        _contextQueue = new ConcurrentQueue<ImportJsonFromFileContext>();
        Task.Run(ImportJsonFile);
        Task.Run(async () => {
            await _completeSemaphore.WaitAsync();
            while (!_contextQueue.IsEmpty)
            {
                await Task.Delay(1000);
            }
            Complete();
        });
    }

    public async Task MarkComplete()
    {
        _complete = true;
        while (!_contextQueue.IsEmpty) { await Task.Delay(1000); }
        _keepRunning = false;
        _completeSemaphore.Release();
    }

    public void ImportSqlFromJson(string jsonFile)
    {
        if (_complete)
        {
            throw new InvalidOperationException("Observable is completed");
        }
        _contextQueue.Enqueue(new ImportJsonFromFileContext(jsonFile));
        _importEvent.Release();
    }


    public IDisposable Subscribe(IObserver<ImportJsonFromFileContext> observer)
    {
        if (_complete)
        {
            throw new InvalidOperationException("Observable is completed");
        }
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
        return new Unsubscriber<ImportJsonFromFileContext>(_observers, observer);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _importEvent.Dispose();
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
    private void Complete()
    {
        foreach (var observer in _observers)
        {
            observer.OnCompleted();
        }
        _observers.Clear();

    }

    private void ImportJsonFile()
    {
        while (_keepRunning)
        {
            _importEvent.Wait();
            while (!_contextQueue.IsEmpty)
            {
                if (_contextQueue.TryPeek(out var context))
                {
                    
                    foreach (var observer in _observers)
                    {
                        Console.WriteLine("import");
                        observer.OnNext(context);
                    }
                    _contextQueue.TryDequeue(out _);
                }
            }
        }
    }

    

    
}

