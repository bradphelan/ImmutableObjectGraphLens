using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ImmutableObjectGraphLens
{
    public static class ExpressionExtensions
    {
        public static object InvokeWithNamedParameters(this MethodBase self, object obj, IDictionary<string, object> namedParameters)
        {
            return self.Invoke(obj, MapParameters(self, namedParameters));
        }

        public static object InvokeWithGeneric(this Type klass, string method, object arg)
        {
            var mi = klass.GetMethod(method);
            var gm = mi.MakeGenericMethod(new[] { arg.GetType() });
            return gm.Invoke(null, new [] {arg});
        }

        public static object[] MapParameters(MethodBase method, IDictionary<string, object> namedParameters)
        {
            string[] paramNames = method.GetParameters().Select(p => p.Name).ToArray();
            object[] parameters = new object[paramNames.Length];
            for (int i = 0; i < parameters.Length; ++i)
            {
                parameters[i] = Type.Missing;
            }
            foreach (var item in namedParameters)
            {
                var paramName = item.Key;
                var paramIndex = Array.IndexOf(paramNames, paramName);
                if(paramIndex!=-1)
                    parameters[paramIndex] = item.Value;
            }
            return parameters;
        }

        public static TTarget Get<TTarget>(this object src, string propName)
        {
            return (TTarget) src.GetType().GetProperty(propName).GetValue(src, null);
        }
    }
}