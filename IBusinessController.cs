using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Joe.Business;

namespace Joe.Web.Mvc
{
    interface IBusinessController
    {
        IRepository BaseRepository { get; }
    }
}
