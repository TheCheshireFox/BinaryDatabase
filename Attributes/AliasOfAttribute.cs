using System;
using System.Collections.Generic;
using System.Text;

namespace BinaryDatabase.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AliasOfAttribute : Attribute
    {
        public string OriginalField { get; }

        public AliasOfAttribute(string field) => OriginalField = field;
    }
}
