﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Joe.Business.Approval;
using Joe.Business.Approval.Views;
using Joe.Business.Approval.Repositories;
using Joe.MapBack;

namespace Joe.Web.Mvc.Controllers.Approval
{
    public abstract class ApprovalController : RepositoryController<Joe.Business.Approval.BusinessApproval, BusinessApprovalView>
    {
        public ApprovalController() : base(new ApprovalRepository()) { }
    }
}
