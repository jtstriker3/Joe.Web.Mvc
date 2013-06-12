using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joe.Web.Mvc
{
    public interface IErrorLogProvider
    {
        void LogError(Exception ex);
    }
}
