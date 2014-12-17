using Joe.MapBack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Joe.Business.Approval;
using Joe.Business.Approval.Views;
using Joe.Initialize;

namespace Joe.Web.Mvc.Controllers.Approval
{
    public abstract class ChangeController<TContext> : Controller
        where TContext : IDBViewContext, new()
    {
        protected Joe.Business.Approval.ApprovalProvider Provider { get; set; }

        public ChangeController()
        {
            Provider = Business.Approval.ApprovalProvider.Instance;
        }

        public ActionResult Approve(Guid changeID, int groupID)
        {
            Provider.ApproveResult(changeID, groupID);
            var changeView = Provider.GetChange(changeID);
            return View(changeView);
        }

        public ActionResult Deny(Guid changeID, int groupID)
        {
            Provider.DenyResult(changeID, groupID);
            var changeView = Provider.GetChange(changeID);
            return View(changeView);
        }

        [HttpPost]
        public ActionResult Submit(ChangeView changeView)
        {
            Provider.SubmitChange(changeView);
            changeView = Provider.GetChange(changeView.ID);
            return View("Details", changeView);
        }

        [HttpGet]
        public ActionResult Submit(Guid id)
        {
            var changeView = Provider.GetChange(id);
            return View(changeView);
        }
    }
}