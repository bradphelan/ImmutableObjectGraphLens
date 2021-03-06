﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;

namespace ImmutableObjectGraphLens.Subjects
{

    public static class SubjectMixins
    {

        /// <summary>
        /// Flatten a sequence of subjects into a single subject. Subscribing
        /// to the output will receive data from the current subject then it
        /// will receive data from the next subject when the switch occurs.
        /// 
        /// Calling OnNext on the output subject will cause the current subject
        /// to receive the OnNext call. When the switch occurs the next subject
        /// will receive the data on a call to OnNext.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="This"></param>
        /// <returns></returns>
        public static ISubject<T> Switch<T>
            (this IObservable<ISubject<T>> This)
        {
            var o = ((IObservable<IObservable<T>>)This).Switch();
            var oo = ((IObservable<IObserver<T>>)This).Switch();
            return new TransformerSubject<T>(oo, o);
        }


        /// <summary>
        /// Calling OnNext on the return observer will call OnNext on whichever
        /// is the latest observer generated by the observable sequence.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IObserver<T> Switch<T>(this IObservable<IObserver<T>> source)
        {
            var current = Observer.Create<T>(x => { });
            var subscription = source.Subscribe(o =>
            {
                // replace with new observer before we complete the old one
                lock (source)
                {
                    current.OnCompleted();
                    current = o;
                }
            });

            return Observer.Create<T>(
                onNext: v => current.OnNext(v),
                onCompleted: () =>
                {
                    lock (source)
                    {
                        subscription.Dispose();
                        current.OnCompleted();
                    }
                },
                onError: e =>
                {
                    lock (source)
                    {
                        subscription.Dispose();
                        current.OnError(e);
                    }
                });
        }

        /// <summary>
        /// Given a dynamic converter provided by the constraintGenerator
        /// and updated by 'u' the observable generate a Subject which always
        /// converts it's values using the latest converter.
        /// </summary>
        /// <typeparam name="Source"></typeparam>
        /// <typeparam name="Target"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="This"></param>
        /// <param name="u"></param>
        /// <param name="constraintGenerator"></param>
        /// <param name="catchWith"></param>
        /// <returns></returns>
        public static ISubject<Target> CombineLatest<Source, Target, U>
            (this ISubject<Source> This
            , IObservable<U> u
            , Func<U, TwoWayConstraint<Source, Target>> constraintGenerator
            , IObserver<Maybe<Exception>> catchWith
            )
        {
            return u.Select(v => This.Select(constraintGenerator(v), catchWith)).Switch();
        }

        /// <summary>
        /// Convert the subject using the converter
        /// </summary>
        public static ISubject<Target> Select<Source, Target>
            (this ISubject<Source> This
            , TwoWayConstraint<Source, Target> converter
            , IObserver<Maybe<Exception>> catchWith
            )
        {
            if (catchWith == null)
            {
                catchWith = Observer.Create<Maybe<Exception>>((e) => {
                    if (e.IsSome)
                    {
                        throw e.Value;
                    }
                });
            }

            return This.Select
                (rightToLeft: converter.ConvertTo
                , leftToRight: converter.ConvertFrom
                , catchWith: catchWith);
        }

        /// <summary>
        /// Bind a lens to a property on an object.
        /// </summary>
        /// <returns></returns>
        public static IDisposable TwoWayBindTo<TObject, TValue>
            (this ILens<TValue> This
            , TObject That
            , Expression<Func<TObject, TValue>> property
            , Func<TValue, bool> validateRight = null)
            where TObject : class
        {
            validateRight = validateRight ?? (t => true);
            return This.Subject.TwoWayBindTo(That.PropertySubject(property), validateRight);
        }

        /// <summary>
        /// Bind a subject to a property on an object
        /// </summary>
        public static IDisposable TwoWayBindTo<TObject, TValue>
            (this ISubject<TValue> This
            , TObject target
            , Expression<Func<TObject, TValue>> property
            , Func<TValue, bool> validateRight = null)
            where TObject : class
        {
            validateRight = validateRight ?? (t => true);

            return This.TwoWayBindTo(target.PropertySubject(property), validateRight);
        }


        /// <summary>
        /// Bind a subject on the left to a subject on the right. It is possible
        /// to supply a validation function to validate the state of the right hand
        /// side.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="validateRight">Perform a validation on the right hand side. If it fails values
        /// will not be passed from right to left. 
        /// 
        /// When a value is passed from left to right the validation is executed after the transfer
        /// </param>
        /// <param name="master"></param>
        /// <returns></returns>
        public static IDisposable TwoWayBindTo<T>
            (this ISubject<T> left
            , ISubject<T> right
            , Func<T, bool> validateRight = null
            )
        {
            // Implement two way binding between the subjects with debounce
            // code
            Maybe<T> lastValue = Maybe<T>.None;
            validateRight = validateRight ?? (t => true);

            bool enableRight = false;


            // Skip the first event from the to so when
            // wiring up the ``from`` subject is the one
            // that defines the initial configuration.
            // TODO. This works when both try to fire
            // events on subscription ( BehaviorSubject )
            // but I'm not sure it works in other cases.
            // Needs testings
            var d1 = right
                .SkipWhile(v => !enableRight)
                .Subscribe(v => {
                    if (!lastValue.IsSome || !EqualityComparer<T>.Default.Equals(v, lastValue.Value))
                    {
                        lastValue = v.ToMaybe();
                        if (validateRight(lastValue.Value))
                            left.OnNext(lastValue.Value);
                    }
                }
                , onError: left.OnError
                , onCompleted: left.OnCompleted
            );

            var d0 = left
                .Subscribe(v =>
                {
                    if (!lastValue.IsSome || !EqualityComparer<T>
                        .Default
                        .Equals
                            (v
                            , lastValue.Value))
                    {
                        lastValue = v.ToMaybe();
                        right.OnNext(lastValue.Value);
                        validateRight(lastValue.Value);
                        if (!enableRight)
                        {
                            enableRight = true;
                        }
                    }
                },
            onError: right.OnError
            , onCompleted: right.OnCompleted);

            return new CompositeDisposable(d0, d1);
        }

        /// <summary>
        /// Transform a subject with two functions
        /// that convert from or to the wrapped type
        /// of the subject.
        ///
        /// If convertTo fails then the optional catchWith
        /// observer is notified with Maybe.Some<Exception>()
        /// 
        /// If convertTo passes without exception catchWith
        /// observer is notified with Maybe.None<Exception>()
        ///
        /// leftToRight is always assumed to pass as this is not
        /// dependant on user input. The catchWith observer is
        /// always notified with Maybe.None<Exception>() whenever
        /// leftToRight is called.
        /// 
        /// It is assumed that leftToRight will always
        /// work.
        /// </summary>
        /// <typeparam name="ConvertedTo"></typeparam>
        /// <typeparam name="ConvertedFrom"></typeparam>
        /// <param name="This"></param>
        /// <param name="convertTo"></param>
        /// <param name="leftToRight"></param>
        /// <param name="catchWith"></param>
        /// <returns></returns>
        public static ISubject<R>
            Select<T, R>
            (this ISubject<T> This
            , Func<R, T> rightToLeft
            , Func<T, R> leftToRight
            , Action<Maybe<Exception>> catchWith
            )
        {
            return This.Select(rightToLeft, leftToRight, Observer.Create(catchWith));
        }

        public static ISubject<R>
            Select<T, R>
            (this ISubject<T> This
            , IObservable<Func<R, T>> rightToLeft
            , IObservable<Func<T, R>> leftToRight
            , IObserver<Maybe<Exception>> catchWith
            )
        {
            var subjectStream = Observable
                .CombineLatest
                (rightToLeft
                , leftToRight
                , (r, l) => This.Select(r, l, catchWith));

            return subjectStream.Switch();
        }

        public static ISubject<R> Select<T, R>
            (this ISubject<T> This
            , Func<R, T> rightToLeft
            , Func<T, R> leftToRight
            , IObserver<Maybe<Exception>> catchWith
            )
        {
            Debug.Assert(catchWith != null);

            // catchWith is always notified with None<Exception>.Default
            // if the leftToRight passes ( which it is assumed to do).
            // This will reset any error state due to an error in
            // user input coming from the opposite direction.
            IObservable<R> observable = This
                .Select(leftToRight)
                .ObserveOn(RxApp.MainThreadScheduler);

            IObserver<R> observer = Observer.Create<R>(
                onNext: x =>
                {
                    try
                    {
                        var v = rightToLeft(x);
                        // catchWith.onNext *must* be called before
                        // This.onNext. If multiple error handlers
                        // are registered then the deepest error
                        // handler should be called last so that
                        // the final state is an error. For example
                        // we might have a parsing stage then a 
                        // validation stage. The parsing stage might
                        // pass and then a Maybe.None<Exception()>
                        // is passed to catchWith to signal success
                        // but then a validation stage might fail.

                        // If the following calls were in the opposite
                        // order then there would be a Maybe.None<Exception()>
                        // sent *after* the deeper Maybe.Some<Exception()> 
                        // which would clear the error state of the
                        // code registered to handle the error state.
                        catchWith.OnNext(None<Exception>.Default);
                        This.OnNext(v);
                    }
                    catch (Exception e)
                    {
                        catchWith.OnNext(e.ToMaybe());
                    }
                },
                onError: x => This.OnError(x));

            return new TransformerSubject<R>(observer, observable);

        }


        /// <summary>
        /// parses a string to enum
        /// formats an enum to string
        /// </summary>
        /// <typeparam name="E">Type of the Enum</typeparam>
        /// <param name="This">Enum subject</param>
        /// <returns>A string subject</returns>
        public static ISubject<string> EnumConverter<E>
            (this ISubject<E> This
            , IObserver<Maybe<Exception>> catchWith
            )
        where E : struct, IConvertible
        {
            return This.Select(
                rightToLeft: (string s) => EnumUtils.ParseEnum<E>(s),
                leftToRight: (E e) => e.ToString(),
                catchWith: catchWith
                );
        }

        /// <summary>
        /// Converts between an enum and it's index
        /// in the enum list. This is not to be confused
        /// with the integer equivalent value of the enum.
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="This"></param>
        /// <param name="catchWith"></param>
        /// <returns></returns>
        public static ISubject<int> EnumIndexConverter<E>
            (this ISubject<E> This
            , IObserver<Maybe<Exception>> catchWith
            )
        {
            var list = EnumUtils.EnumToList<E>();
            return This.Select
                (leftToRight: e => list.IndexOfEnum(e)
                , rightToLeft: i => list[i].Item1
                , catchWith: catchWith
                );
        }

        /// <summary>
        /// Casts the subject(of E) to subject(of Object)
        /// 
        /// This is usefull for binding to SelectedItem on
        /// lists which require and object for binding
        /// 
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <typeparam name="ConvertedTo"></typeparam>
        /// <param name="This"></param>
        /// <returns></returns>
        public static ISubject<T> Cast<E, T>(
            this ISubject<E> This)
            where E : class
            where T : class
        {
            var catchWith = new Subject<Maybe<Exception>>();
            return This.Select
                (rightToLeft: (T s) => s as E
                , leftToRight: (E e) => e as T
                , catchWith: catchWith
                );
        }

        /// <summary>
        /// Use standard converters to convert the types from
        /// the source subject to target subject
        /// </summary>
        /// <typeparam name="Source"></typeparam>
        /// <typeparam name="Target"></typeparam>
        /// <param name="subject"></param>
        /// <param name="catchWith"></param>
        /// <returns></returns>
        public static ISubject<Target> Convert<Source, Target>
            (this ISubject<Source> subject
            , IObserver<Maybe<Exception>> catchWith)
        {

            Contract.Requires(catchWith != null, "catchWith param cannot be null");

            var targetType = typeof(Target);
            var sourceType = typeof(Source);

            Func<Source, Target> sourceToTarget;
            Func<Target, Source> targetToSource;
            Converters<Source, Target>(targetType, sourceType, out sourceToTarget, out targetToSource);

            return subject.Select
                (rightToLeft: targetToSource
                , leftToRight: sourceToTarget
                , catchWith: catchWith
                );
        }


        public static void Converters<Source, Target>
            (Type targetType, Type sourceType, out Func<Source, Target> sourceToTarget, out Func<Target, Source> targetToSource)
        {
            if (sourceType == typeof(string))
            {
                TypeConverter targetConverter = TypeDescriptor.GetConverter(typeof(Target));
                if (!targetConverter.CanConvertTo(typeof(string))
                    || !targetConverter.CanConvertFrom(typeof(string)))
                {
                    throw new ArgumentException("Unable to convert " + sourceType + " <--> " + targetType);
                }
                sourceToTarget = source => (Target)targetConverter.ConvertFrom(source);
                targetToSource = target => (Source)targetConverter.ConvertTo(target, typeof(Source));
            }
            else
            {
                TypeConverter sourceConverter = TypeDescriptor.GetConverter(typeof(Source));
                if (!sourceConverter.CanConvertTo(typeof(Target))
                    || !sourceConverter.CanConvertFrom(typeof(Target)))
                {
                    throw new ArgumentException("Unable to convert " + sourceType + " <--> " + targetType);
                }
                sourceToTarget = source => (Target)sourceConverter.ConvertTo(source, typeof(Target));
                targetToSource = target => (Source)sourceConverter.ConvertFrom(target);
            }
        }

        /// <summary>
        /// Generate a subject from an INPC property
        /// </summary>
        public static ISubject<TValue> PropertySubject<TObject, TValue>
            (this TObject This
            , Expression<Func<TObject, TValue>> property
            )
            where TObject : class
        {
            if (This == null) throw new ArgumentNullException(nameof(This));
            if (property == null) throw new ArgumentNullException(nameof(property));

            IObservable<TValue> o = This.WhenAnyValue(property).Retry();

            var propertyExpression = ReactiveUI.Reflection.Rewrite(property.Body);
            IObserver<TValue> oo = Observer.Create<TValue>
                (x =>
                {
                    //try
                    //{
                    ReactiveUI.Reflection.TrySetValueToPropertyChain
                        (This, propertyExpression.GetExpressionChain(), x, true);
                    //}
                    //catch (TargetInvocationException e)
                    //{
                    //    // Null reference exception ??
                    //    Console.WriteLine(e);
                    //}
                });

            return new TransformerSubject<TValue>(oo, o);
        }


    }
}
