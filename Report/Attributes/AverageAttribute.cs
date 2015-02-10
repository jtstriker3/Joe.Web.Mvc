using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Joe.Web.Mvc.Report.Attributes
{
    public class AverageAttribute : Attribute
    {
        public bool IsPercent { get; private set; }
        public int Precision { get; set; }

        public AverageAttribute()
        {
            Precision = 2;
        }

        public AverageAttribute(bool isPercent, int precision = 2)
        {
            IsPercent = isPercent;
            Precision = precision;
        }
    }
}