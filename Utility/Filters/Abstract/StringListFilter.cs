using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utility.Filters.Abstract
{
    public abstract class StringListFilter<T> : TrueFilter<T> where T : struct
    {
        private List<string> list;
        protected List<string> List => list;
        public StringListFilter(List<string> list) => this.list = list;
    }
}
