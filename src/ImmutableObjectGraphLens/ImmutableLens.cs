using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using ImmutableObjectGraph;

namespace ImmutableObjectGraphLens
{
    public interface IImmutableLens<TRoot, TTarget>
    {
        TRoot Set(TRoot root, TTarget value);
        TTarget Get(TRoot root);
    }

    public class ImmutableLens<TRoot, TTarget> : IImmutableLens<TRoot, TTarget>
    {
        private readonly Expression<Func<TRoot, TTarget>> _Selector;
        private readonly Func<TRoot, TTarget> _CompiledSelector;

        public ImmutableLens(Expression<Func<TRoot, TTarget>> selector )
        {
            _Selector = selector;
            _CompiledSelector = selector.Compile();
        }

        public TRoot Set(TRoot root, TTarget value)
        {
            return root.WithProps(value, _Selector);
        }

        public TTarget Get(TRoot root)
        {
            return _CompiledSelector(root);

        }
    }
       
    public static class ImmutableLens
    {
        public static ImmutableLens<TRoot, TTarget> CreateLens<TRoot,TTarget>
            (this Expression<Func<TRoot, TTarget>> selector)
        {
            return new ImmutableLens<TRoot, TTarget>(selector);
        }

        public static TRoot With<TRoot,TTarget>(this TRoot root, string prop, TTarget target)
        {
            var method = root.GetType().GetMethod("With");
            return (TRoot) method
                .InvokeWithNamedParameters(root, new Dictionary<string, object>() {{prop, typeof(Optional).InvokeWithGeneric("For", target)}});
        }
        public static TRoot WithProps<TRoot,TTarget>(this TRoot root, TTarget target, params string [] props)
        {
            var nodes = props
                .SkipLast(1)
                .Scan((object) root, (o, prop) => o.Get<object>(FirstCharToUpper(prop)))
                .StartWith(root)
                .Reverse()
                .ToList();

            props = props.Reverse().ToArray();

            return (TRoot) nodes
                .Zip(props, (n, p) => new {n, p})
                .Aggregate((object) target, (value, o) => o.n.With(o.p, value));
            
        }

        public static TRoot WithProps<TRoot, TTarget> (this TRoot root , TTarget target, Expression<Func<TRoot, TTarget>> selector)
        {
            var props = ReactiveUI.Reflection.ExpressionToPropertyNames(selector.Body)
                .Split('.')
                .Select(s=>s.ToLower(CultureInfo.InvariantCulture))
                .ToArray();
            return WithProps(root, target, props);

        }

        private static string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + String.Join("", input.Skip(1));
        }
    }
}