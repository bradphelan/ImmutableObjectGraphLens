using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    /// <typeparam name="O"></typeparam>
    /// <typeparam name="Source"></typeparam>
    public class PropertyLens<O, Source> : ReactiveObject, ILens<Source>
        where O : class, INotifyPropertyChanged
        where Source : class
    {
        private Expression<Func<O, Source>> _Selector;
        private O _Root;
        private Func<O, Source> _CompiledSelector;
        private ImmutableLens<O, Source> _ImmutableLens;

        public ILens<Prop> Focus<Prop>(Expression<Func<Source, Prop>> selector)
        {
            return new Lens<Source, Prop>(this, selector);
        }

        public Source Current
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
            }
        }

        public object Root => this;

        public PropertyLens(O root, Expression<Func<O, Source>> selector)
        {
            _ImmutableLens = selector.CreateLens();
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
        /// Create a lens for a mutable property. The value
        /// of the property should be immutable. 
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="o"></param>
        /// <param name="selector">The expression should reference a property on the source object. Nested property
        /// references will cause a runtime error. </param>
        /// <returns></returns>
        public static ILens<TProperty> PropertyLens<TObject, TProperty>
            (this TObject o
            , Expression<Func<TObject, TProperty>> selector
            )
            where TObject : class, INotifyPropertyChanged
            where TProperty : class
        {
            return new PropertyLens<TObject, TProperty>(o, selector);
        }

        public static ILens<ImmutableList<TProperty>> PropertyLens<TObject, TProperty>
            (this TObject o
            , Expression<Func<TObject, ImmutableList<TProperty>>> selector
            )
            where TObject : class, INotifyPropertyChanged
            where TProperty : class
        {
            return new PropertyLens<TObject, ImmutableList<TProperty>>(o, selector);
        }
    }
}
