using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Czf.Socrata.APIDownloader.Observables;

/// <summary>
/// Base on https://docs.microsoft.com/en-us/dotnet/standard/events/how-to-implement-a-provider
/// </summary>
internal class Unsubscriber<T>: IDisposable
{
    private List<IObserver<T>> _observers;
    private IObserver<T> _observer;

    public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
    {
        this._observers = observers;
        this._observer = observer;
    }

    public void Dispose()
    {
        if (!(_observer == null)) _observers.Remove(_observer);
    }
    
}
