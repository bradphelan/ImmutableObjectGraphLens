using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using ImmutableObjectGraphLens;
using ImmutableObjectGraphLens.Subjects;
using ReactiveUI;

namespace Weingartner.Lens
{
    /// <summary>
    /// A Lens onto INPC property that holds an immutable object
    /// </summary>
    /// <typeparam name="TRoot"></typeparam>
    /// <typeparam name="TImmutable"></typeparam>
    public class PropertyLens<TRoot, TImmutable> : ReactiveObject, ILens<TImmutable>
        where TRoot : class, INotifyPropertyChanged
        where TImmutable : class
    {
        private readonly Expression<Func<TRoot, TImmutable>> _Selector;
        private readonly TRoot _Root;
        private readonly Func<TRoot, TImmutable> _CompiledSelector;

        public ILens<TProp> Focus<TProp>(Expression<Func<TImmutable, TProp>> selector)
        {
            return new Lens<TImmutable, TProp>(this, selector);
        }

        public TImmutable Current
        {
            get
            {
                return _CompiledSelector(_Root);
            }
            set
            {
                var prop = (PropertyInfo)((MemberExpression)_Selector.Body).Member;
                prop.SetValue(_Root, value, null);
                this.RaisePropertyChanged(prop.Name);
                this.RaisePropertyChanged();
            }
        }

        public object Root => this;

        public PropertyLens(TRoot root, Expression<Func<TRoot, TImmutable>> selector)
        {
            selector.CreateLens();
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var names = ReactiveUI.Reflection.ExpressionToPropertyNames(selector.Body);

            if (names.Contains('.')) throw new ArgumentException("Selector may only be depth 1", nameof(selector));
            _Root = root;
            _Selector = selector;
            _CompiledSelector = _Selector.Compile();
        }
    }

    public static class PropertyLensMixins
    {
        /// <summary>
        /// Create a property lens based on a mutable property of root. The type of the property
        /// should be an immutable property.
        /// </summary>
        /// <typeparam name="TRoot"></typeparam>
        /// <typeparam name="TProp"></typeparam>
        /// <param name="root"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static PropertyLens<TRoot, TProp> Focus<TRoot, TProp>(this TRoot root, Expression<Func<TRoot, TProp>> selector)
            where TRoot : class, INotifyPropertyChanged
            where TProp : class

        {
            return new PropertyLens<TRoot, TProp>(root, selector);
        }
    }
}
