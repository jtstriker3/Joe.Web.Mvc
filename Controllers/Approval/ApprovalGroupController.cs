using Joe.Business.Approval.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Joe.Business.Approval.Repositories;
using Joe.MapBack;

namespace Joe.Web.Mvc.Controllers.Approval
{

    public abstract class ApprovalGroupController<TContext> : RepositoryController<Joe.Business.Approval.ApprovalGroup, ApprovalGroupView>
         where TContext : IDBViewContext, new()
    {
        public ApprovalGroupController() : base(new ApprovalGroupRepository<TContext>()) { }
    }

}
