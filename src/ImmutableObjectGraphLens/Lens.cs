using System.Reactive.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace ImmutableObjectGraphLens
{
    public interface IHasRoot
    {
        /// <summary>
        /// Return the root object for the lens. This
        /// is the container that the Lens is based on
        /// most likely the undo stack.
        /// </summary>
        object Root { get; }
    }
    public interface ILeafLens<Prop> : IHasRoot
    {

        /// <summary>
        /// The current value of the node
        /// </summary>
        Prop Current { get; set; }


    }

    public interface ILens<Prop> : ILeafLens<Prop>
    {
        /// <summary>
        /// Produce a lens onto a sub node via a selector
        /// </summary>
        /// <typeparam name="SubProp"></typeparam>
        /// <param name="selector"></param>
        /// <returns></returns>
        ILens<SubProp> Focus<SubProp>(Expression<Func<Prop, SubProp>> selector);
    }


    public interface IDeletableLens<Prop> : ILens<Prop>
    {
        /// <summary>
        /// Delete the resource associated with this lens. After
        /// calling this the lens should be invalid.
        /// </summary>
        void Delete();

        /// <summary>
        /// Return true if the 
        /// </summary>
        /// <returns></returns>
        bool Exists();
    }

    public static class LensExtensions
    {
        /// <summary>
        /// Not much different from Subject except that we
        /// are making it clean this is a one way event
        /// stream.
        /// </summary>
        /// <typeparam name="Prop"></typeparam>
        /// <typeparam name="SubProp"></typeparam>
        /// <param name="lens"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IObservable<SubProp> Observe<Prop, SubProp>
            (this ILens<Prop> lens
            , Func<Prop, SubProp> selector )
        {
            return lens.WhenAnyValue(p => p.Current).Select(selector);
        }

        #region WhenAny like behaviours
        public static IObservable<P0> DataWhenAnyValue<Prop, P0>
            ( this ILens<Prop> @this
            , Func<Prop, P0> selector0
            )
        {
            return @this.Observe(selector0);
        }
        public static IObservable<TResult> DataWhenAnyValue<Prop, P0,TResult>
            ( this ILens<Prop> @this
            , Func<Prop, P0> selector0
            , Func<P0,TResult> fn
            )
        {
            return @this.Observe(selector0).Select(fn);
        }
        public static IObservable<TResult> DataWhenAnyValue<Prop, P0,P1,TResult>
            ( this ILens<Prop> @this
            , Func<Prop, P0> selector0
            , Func<Prop, P1> selector1
            , Func<P0,P1,TResult> fn
            )
        {
            return Observable
                .CombineLatest
                    ( @this.Observe(selector0)
                    , @this.Observe(selector1)
                    , fn
                    );
        }
        public static IObservable<TResult> DataWhenAnyValue<Prop, P0,P1,P2,TResult>
            ( this ILens<Prop> @this
            , Func<Prop, P0> selector0
            , Func<Prop, P1> selector1
            , Func<Prop, P2> selector2
            , Func<P0,P1,P2,TResult> fn
            )
        {
            return Observable
                .CombineLatest
                    ( @this.Observe(selector0)
                    , @this.Observe(selector1)
                    , @this.Observe(selector2)
                    , fn
                    );
        }
        public static IObservable<TResult> DataWhenAnyValue<Prop, P0,P1,P2,P3,TResult>
            ( this ILens<Prop> @this
            , Func<Prop, P0> selector0
            , Func<Prop, P1> selector1
            , Func<Prop, P2> selector2
            , Func<Prop, P3> selector3
            , Func<P0,P1,P2,P3,TResult> fn
            )
        {
            return Observable
                .CombineLatest
                    ( @this.Observe(selector0)
                    , @this.Observe(selector1)
                    , @this.Observe(selector2)
                    , @this.Observe(selector3)
                    , fn
                    );
        }
        #endregion

    }


    /// <summary>
    /// A lens that does nothing to be used as a default lens
    /// when nothing is available
    /// </summary>
    /// <typeparam name="Prop"></typeparam>
    internal class EmptyLens<Prop> : ILens<Prop>
    {
        public ISubject<Prop> Subject
        {
            get { return new Subject<Prop>(); }
        }

        public object Root { get { return this; } }

        public ILens<SubProp> Focus<SubProp>(Expression<Func<Prop, SubProp>> selector)
        {
            return Lens.Empty<SubProp>();
        }

        public Prop Current
        {
            get { return default(Prop); }
            set { Subject.OnNext(value); }
        }

        public ILeafLens<SubProp> Focus<SubProp>(Func<ISubject<Prop>, ISubject<SubProp>> transform)
        {
            return Lens.Empty<SubProp>();
        }

        public List<string> Properties
        {
            get { return new List<string>(); }
        }
    }

    /// <summary>
    /// Mixins for the ILens interface
    /// </summary>
    public static partial class Lens
    {
        /// <summary>
        /// Generate an empty Lens
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ILens<T> Empty<T>()
        {
            return new EmptyLens<T>();
        }

        /// <summary>
        /// A helper utility for registering dependency properties than are
        /// based on ILens
        /// </summary>
        /// <typeparam name="LensProperty"></typeparam>
        /// <typeparam name="Element"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public static System.Windows.DependencyProperty
            RegisterDependencyProperty<LensProperty,Element>
            (string name)
        {
            return System.Windows.DependencyProperty.Register
                ( name
                , typeof(ILeafLens<LensProperty>)
                , typeof(Element)
                , new System.Windows.PropertyMetadata(Lens.Empty<LensProperty>())
                );
        }

    }


    public class LensException : Exception
    {
        public LensException(string message, Exception inner = null) : base(message, inner)
        { }
    };

    public class Lens<TRoot, TProperty> : ReactiveObject, ILens<TProperty>
    {
        private readonly ILens<TRoot> _Root;
        private readonly IImmutableLens<TRoot, TProperty> _ImmutableLens;

        public Lens(ILens<TRoot> root, IImmutableLens<TRoot, TProperty> immutableLens )
        {
            _Root = root;
            _ImmutableLens = immutableLens;
        }
        public Lens(ILens<TRoot> root, Expression<Func<TRoot, TProperty>> immutableLens )
        {
            _Root = root;
            _ImmutableLens = immutableLens.CreateLens();
        }


        public TProperty Current {
            get { return _ImmutableLens.Get(_Root.Current); }
            set
            {
                var current = _Root.Current;
                _Root.Current = _ImmutableLens.Set(current, value);
                this.RaisePropertyChanged("Current"); 
            }
        }

        public ILens<SubProp> Focus<SubProp>(Expression<Func<TProperty, SubProp>> selector)
        {
            return new Lens<TProperty, SubProp>(this, selector.CreateLens());
        }

        public object Root => _Root;
    }

    public class Reflection
    {

        static public Expression<Func<R, O>> CreatePropSelectorExpression<R, O>(IEnumerable<string> propertyNames)
        {
            var key = String.Join(".", propertyNames);
            ParameterExpression parameter = Expression.Parameter(typeof(R));
            Expression selector = propertyNames.Aggregate((Expression)parameter, (a, name) => Expression.PropertyOrField(a, name));
            return Expression.Lambda<Func<R, O>>(selector, parameter);
        }
    }



}
