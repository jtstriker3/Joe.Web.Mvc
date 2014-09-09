using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Joe.Web.Mvc
{
    public static class ErrorLogger
    {
        public static IErrorLogProvider LogProvider { get; set; }

        public static void LogError(Exception ex)
        {
            if (LogProvider != null)
                LogProvider.LogError(ex);
        }
    }
}