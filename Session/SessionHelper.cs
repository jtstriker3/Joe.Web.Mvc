using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.SessionState;


namespace Joe.Web.Mvc.Utility.Session
{
    public static class SessionHelper
    {
        public static HttpSessionState Session
        {
            get
            {
                return HttpContext.Current.Session;
            }
        }

        public static String ManyToManyFocus
        {
            get
            {
                return Session["ManyToManyFocus"] as String;
            }
            set
            {
                Session["ManyToManyFocus"] = value;
            }
        }
    }
}