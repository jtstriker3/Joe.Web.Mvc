using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Web.Configuration;
using Joe.Web.Mvc.Utility.Extensions;

namespace Joe.Web.Mvc.Utility.Configuration
{
    public static class ConfigurationHelper
    {
        public static Boolean Debug
        {
            get
            {
                return ((CompilationSection)System.Configuration.ConfigurationManager.GetSection("system.web/compilation")).Debug;
            }
        }

        public static String DefaultErrorpage
        {
            get
            {
                return ((CustomErrorsSection)System.Configuration.ConfigurationManager.GetSection("system.web/customErrors")).DefaultRedirect;
            }
        }

        public static int PageSize
        {
            get
            {
                var value = System.Web.Configuration.WebConfigurationManager.AppSettings["PageSize"] ?? 10.ToString();
                return Convert.ToInt32(value);
            }
        }

        public static String AdminRole
        {
            get
            {
                return System.Web.Configuration.WebConfigurationManager.AppSettings["AdminRole"] ?? "Administrators";
            }
        }

        public static String AdminArea
        {
            get
            {
                var adminArea = System.Web.Configuration.WebConfigurationManager.AppSettings["AdminArea"] as String;
                return adminArea.NotNull() ? adminArea + "/" : String.Empty;
            }
        }
    }
}