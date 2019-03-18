using System;
using System.Collections.Generic;
using System.Text;

namespace Utility.Filters
{
    public class TrueFilter<T> : IFilter<T>
        where T : struct
    {
        public virtual bool ApplyFilter(T obj)
        {
            return true;
        }

        public IFilter<T1> OfType<T1>() where T1 : struct
        {
            return (IFilter<T1>)this;
        }
    }
}
