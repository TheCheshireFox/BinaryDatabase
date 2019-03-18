using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utility.Filters
{
    public class PropRegexFilter<T> : Abstract.RegexFilter<T> where T : struct
    {
        private Func<T, object> getter;

        public PropRegexFilter(string propName, string rxString) : base(rxString)
        {
            var pi = typeof(T).GetProperty(propName);
            var fi = typeof(T).GetField(propName);

            if (pi != null) getter = (o) => pi.GetValue(o);
            else getter = (o) => fi.GetValue(o);
        }

        public override bool ApplyFilter(T obj) => Regex.IsMatch(getter(obj).ToString());
    }
}
