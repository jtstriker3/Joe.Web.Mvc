using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Joe.Web.Mvc
{
    public class AjaxActionData
    {
        public String Type { get; private set; }
        public Object Data { get; private set; }

        public AjaxActionData(String type, Object data)
        {
            Type = type;
            Data = data;
        }
    }
}