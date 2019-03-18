using System;
using System.Collections.Generic;
using System.Text;

namespace Utility.Filters
{
    public interface IFilter
    {
        IFilter<T> OfType<T>() where T : struct;
    }

    public interface IFilter<T> : IFilter
        where T : struct
    {
        bool ApplyFilter(T obj);
    }
}
