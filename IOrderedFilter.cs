using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Joe.Web.Mvc
{
    interface IOrderedFilter
    {
        int Order { get; }
    }
}
