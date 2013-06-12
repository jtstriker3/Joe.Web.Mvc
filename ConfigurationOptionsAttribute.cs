using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Joe.Web.Mvc
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MvcOptionsAttribute : Attribute
    {
        public Boolean TrueDelete { get; set; }
        public Boolean ShowSubMenu { get; set; }
        public Boolean ShowCreate { get; set; }
        public Boolean IndexAjax { get; set; }
        public String CreateErrorMessage { get; set; }
        public String EditErrorMessage { get; set; }
        public String IndexErrorMessage { get; set; }
        public String DeleteErrorMessage { get; set; }
        public String RedirectOnCreate { get; set; }
        public Boolean PassIDOnCreateRedirect { get; set; }
        public String RedirectOnDelete { get; set; }
        public Boolean ReturnErrorViewOnError { get; set; }

        public MvcOptionsAttribute()
        {
            PassIDOnCreateRedirect = true;
            ReturnErrorViewOnError = true;

        }
    }
}