using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Joe.Web.Mvc
{
    public class BaseErrorController : Controller
    {
        protected override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            ViewBag.Error = true;
            ViewBag.Success = false;
            ViewBag.ErrorText = Session["ErrorText"];
            Session.Remove("ErrorText");
            base.OnResultExecuting(filterContext);
        }
    }
}
