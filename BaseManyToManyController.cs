using System;
using System.Collections.Generic;
using System.Data.Objects.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Joe.Map;
using System.Reflection;
using Joe.Web.Mvc;
using Joe.Business;
using Joe.Web.Mvc.Utility.Session;
using Joe.MapBack;
namespace Joe.Web.Mvc
{
    public abstract class BaseManyToManyController<TModel, TViewModel, TRelationModel1, TRelationModel2, TRelationView1, TRelationView2, TRepository>
        : BaseSingleIDController<TModel, TViewModel, TRepository>
        where TModel : class, new()
        where TViewModel : class, IManyToMany, new()
        where TRelationView1 : class,  new()
        where TRelationView2 : class,  new()
        where TRelationModel1 : class, new()
        where TRelationModel2 : class, new()
        where TRepository : class, IDBViewContext, new()
    {
        public BaseManyToManyController(IBusinessObject<TModel, TViewModel, TRepository> businessObject)
            : base(businessObject)
        {
        }

        public override ActionResult Index()
        {
            Utility.Session.SessionHelper.ManyToManyFocus = "N";
            return base.Index();
        }

        public virtual ActionResult IndexBy1(String id)
        {
            try
            {
                Utility.Session.SessionHelper.ManyToManyFocus = "1";
                var relationView = Joe.Business.BusinessObject<TRelationModel1, TRelationView1, TRepository>.QuickGet(id);

                var intID = Convert.ToInt32(id);
                ViewBag.FocusName = (String)relationView.GetType().GetProperty("Name").GetValue(relationView, null);
                if (Options.IndexAjax)
                    return Request.IsAjaxRequest() ? this.PartialView("Index", new List<TViewModel>()) : (ActionResult)this.View("Index", new List<TViewModel>());
                else
                {
                    var viewModelList = this.BusinessObject.Get().Where(v => v.ID1 == intID);
                    if (ViewModelListRetrived != null)
                        viewModelList = ViewModelListRetrived(viewModelList);
                    return Request.IsAjaxRequest() ? this.PartialView("Index", viewModelList) : (ActionResult)this.View("Index", viewModelList);
                }
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        public virtual ActionResult IndexBy2(String id)
        {
            try
            {
                var relationView = Joe.Business.BusinessObject<TRelationModel2, TRelationView2, TRepository>.QuickGet(id);
                Utility.Session.SessionHelper.ManyToManyFocus = "2";
                var intID = Convert.ToInt32(id);
                ViewBag.FocusName = (String)relationView.GetType().GetProperty("Name").GetValue(relationView, null);

                if (Options.IndexAjax)
                    return Request.IsAjaxRequest() ? this.PartialView("Index", new List<TViewModel>()) : (ActionResult)this.View("Index", new List<TViewModel>());
                else
                {
                    var viewModelList = this.BusinessObject.Get().Where(v => v.ID2 == intID);
                    if (ViewModelListRetrived != null)
                        viewModelList = ViewModelListRetrived(viewModelList);
                    return Request.IsAjaxRequest() ? this.PartialView("Index", viewModelList) : (ActionResult)this.View("Index", viewModelList);
                }
                //return this.PartialView("Index", new List<TViewModel>());
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        public ActionResult Create(string id)
        {
            try
            {
                TViewModel viewModel = this.InitCreateModel();
                SessionHelper.ManyToManyFocus = Request.QueryString["indexBy"] ?? SessionHelper.ManyToManyFocus;
                if (Convert.ToInt32(id) != -1)
                {
                    if (SessionHelper.ManyToManyFocus == "1")
                    {

                        var relationView = Joe.Business.BusinessObject<TRelationModel1, TRelationView1, TRepository>.QuickGet(id);

                        String relationName = (String)relationView.GetType().GetProperty("Name").GetValue(relationView, null);
                        viewModel.ID1 = (int)Convert.ChangeType(id, viewModel.ID1.GetType());
                        viewModel.Name1 = relationName;
                    }
                    else
                    {
                        var relationView = Joe.Business.BusinessObject<TRelationModel2, TRelationView2, TRepository>.QuickGet(id);

                        String relationName = (String)relationView.GetType().GetProperty("Name").GetValue(relationView, null);
                        viewModel.ID2 = (int)Convert.ChangeType(id, viewModel.ID1.GetType());
                        viewModel.Name2 = relationName;
                    }
                }
                return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        [ActionName("CreateWithNoID")]
        public override ActionResult Create()
        {
            return base.Create();
        }

        [ActionName("EditOneID")]
        public override ActionResult Edit(string id)
        {
            return base.Edit(id);
        }

        public virtual ActionResult Edit(String id, String id2)
        {
            try
            {
                SessionHelper.ManyToManyFocus = Request.QueryString["indexBy"] ?? SessionHelper.ManyToManyFocus;
                var viewModel = this.BusinessObject.Get(id, id2);
                if (ViewModelRetrieved != null)
                    ViewModelRetrieved(viewModel, id, id2);
                return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        [ActionName("Edit3ID")]
        public virtual ActionResult Edit(String id, String id2, String id3)
        {
            try
            {
                SessionHelper.ManyToManyFocus = Request.QueryString["indexBy"] ?? SessionHelper.ManyToManyFocus;

                var viewModel = this.BusinessObject.Get(id, id2, id3);
                if (ViewModelRetrieved != null)
                    ViewModelRetrieved(viewModel, id, id2, id3);
                return Request.IsAjaxRequest() ? PartialView("Edit", viewModel) : (ActionResult)this.View("Edit", viewModel);
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        [ActionName("DeleteOneID")]
        public override ActionResult Delete(string id)
        {
            return base.Delete(id);
        }

        public virtual ActionResult Delete(String id, String id2)
        {
            var viewModel = this.BusinessObject.Get(id, id2);
            return this.Delete(viewModel);
        }

        protected override ActionResult CreateResult(TViewModel viewModel)
        {
            return RedirectToIndex(viewModel);
        }

        protected virtual ActionResult RedirectToIndex(TViewModel viewModel)
        {
            switch (SessionHelper.ManyToManyFocus)
            {
                case "1":
                    return RedirectToAction("IndexBy1", new { id = viewModel.ID1 });
                case "2":
                    return RedirectToAction("IndexBy2", new { id = viewModel.ID2 });
                default:
                    return RedirectToAction("Index");
            }
        }

        protected override TViewModel InitCreateModel()
        {
            return new TViewModel();
        }

    }
}
