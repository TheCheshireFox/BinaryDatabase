using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utility.Filters
{
    public class PropIntListFilter<T> : Abstract.IntListFilter<T> where T : struct
    {
        private Func<T, object> getter;

        public PropIntListFilter(string propName, List<int> s) : base(s)
        {
            var pi = typeof(T).GetProperty(propName);
            var fi = typeof(T).GetField(propName);

            if (pi != null) getter = (o) => pi.GetValue(o);
            else getter = (o) => fi.GetValue(o);
        }

        public override bool ApplyFilter(T obj) => List.IndexOf((int)getter(obj)) != -1;
    }
}
