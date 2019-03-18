using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Utility.Filters.Abstract
{
    public abstract class RegexFilter<T> : TrueFilter<T> where T : struct
    {
        private Regex regex;
        protected Regex Regex => regex;
        public RegexFilter(string rxString) => regex = new Regex(rxString);
    }
}
