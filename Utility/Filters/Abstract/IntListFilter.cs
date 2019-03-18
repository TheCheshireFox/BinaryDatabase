using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utility.Filters.Abstract
{
    public abstract class IntListFilter<T> : TrueFilter<T> where T : struct
    {
        private List<int> list;
        protected List<int> List => list;
        public IntListFilter(List<int> list) => this.list = list;
    }
}
