using System;
using System.Reactive.Subjects;

namespace ImmutableObjectGraphLens.Subjects
{
    /// <summary>
    /// This is a wrapper for independant IObserver and IObservable to be
    /// combined into an ISubject. All the methods are just forwarded
    /// to the respective object.
    /// </summary>
    /// <typeparam name="ConvertedTo"></typeparam>
    public class TransformerSubject<T> : ISubject<T>
    {
        private IObserver<T> _observer;
        private IObservable<T> _observable;

        public TransformerSubject(IObserver<T> observer, IObservable<T> observable)
        {
            _observer = observer;
            _observable = observable;
        }

        public void OnCompleted()
        {
            _observer.OnCompleted();
        }

        public void OnError(Exception error)
        {
            _observer.OnError(error);
        }

        public void OnNext(T value)
        {
            _observer.OnNext(value);
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            return _observable.Subscribe(observer);
        }
    }
}
