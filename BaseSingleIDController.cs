using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Reflection;
using Joe.Map;
using Joe.Web.Mvc;
using Joe.Business;
using System.Linq.Expressions;
using Joe.Web.Mvc.Utility.Extensions;
using Joe.MapBack;
using Joe.Reflection;

namespace Joe.Web.Mvc
{
    public abstract class BaseSingleIDController<TModel, TViewModel, TRepository> : BaseController
        where TModel : class, new()
        where TViewModel : class, new()
        where TRepository : class, IDBViewContext, new()
    {
        protected MvcOptionsAttribute Options { get; set; }
        public IRepository<TModel, TViewModel, TRepository> Repository { get; set; }
        public delegate TViewModel GetDelegate(TViewModel viewModel, params String[] ids);
        public delegate IQueryable<TViewModel> GetListDelegate(IQueryable<TViewModel> viewModelList);
        public GetDelegate ViewModelRetrieved;
        public GetListDelegate ViewModelListRetrived;


        public BaseSingleIDController(IRepository<TModel, TViewModel, TRepository> businessObject)
            : base(businessObject)
        {
            Options = (MvcOptionsAttribute)GetType().GetCustomAttributes(typeof(MvcOptionsAttribute), true).SingleOrDefault() ?? new MvcOptionsAttribute();

            ViewBag.TrueDelete = Options.TrueDelete;
            ViewBag.ShowSubMenu = Options.ShowSubMenu;
            ViewBag.ShowCreate = Options.ShowCreate;

            Repository = businessObject;
        }

        public virtual ActionResult Index()
        {
            return this.Index(null);
        }

        [HttpPost]
        public virtual ActionResult Index(String filter)
        {
            string filterString = null;
            if (!String.IsNullOrWhiteSpace(filter))
            {
                foreach (var filterProp in Options.FilterProperties)
                {
                    if (!filterString.NotNull())
                        filterString = filterProp + ":Contains:" + filter;
                    else
                        filterString += ":or:" + filterProp + ":Contains:" + filter;
                }
            }
            return Index(null, filterString: filterString);
        }

        protected virtual ActionResult Index(Expression<Func<TViewModel, Boolean>> filter, Object dynamicFilters = null, String filterString = null)
        {
            try
            {
                if (Options.IndexAjax)
                    return Request.IsAjaxRequest() ? PartialView() : (ActionResult)this.View();
                else
                {
                    int? take, skip;
                    string orderBy, where;
                    Boolean descending = false;
                    take = filterString.NotNull() ? null : Request.QueryString["take"].ToNullable<int>();
                    skip = filterString.NotNull() ? null : Request.QueryString["skip"].ToNullable<int>();
                    orderBy = Convert.ToString(Request.QueryString["orderby"]);
                    descending = Convert.ToBoolean(Request.QueryString["descending"]);
                    where = filterString ?? Convert.ToString(Request.QueryString["where"]);
                    int count;
                    var viewModelList = this.Repository.Get(out count, filter, take.HasValue ? take : Options.DefaultPageSize, skip, descending: descending, orderBy: orderBy, stringFilter: where, dynamicFilter: dynamicFilters);
                    ViewBag.Count = count;
                    ViewBag.Take = take.HasValue ? take.Value : Options.DefaultPageSize;
                    ViewBag.Skip = skip.HasValue ? skip.Value : 0;
                    ViewBag.OrderBy = orderBy;
                    ViewBag.Descending = descending;
                    ViewBag.Where = where;
                    if (ViewModelListRetrived != null)
                        viewModelList = ViewModelListRetrived(viewModelList);
                    return Request.IsAjaxRequest() ? PartialView(viewModelList) : (ActionResult)this.View(viewModelList);
                }
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        public virtual ActionResult Details(String id)
        {
            TViewModel viewModel = null;
            id = this.Decode(id).Single();
            try
            {
                viewModel = this.Repository.Get(id);
            }
            catch (Exception ex)
            {
                ViewBag.NoModel = true;
                return Error(ex, Options);
            }
            if (ViewModelRetrieved != null)
                viewModel = ViewModelRetrieved(viewModel, id);
            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
        }

        public virtual ActionResult Create()
        {

            var viewModel = this.InitCreateModel();
            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
        }

        [ActionName("CreateWithID")]
        public virtual ActionResult Create(string id)
        {
            try
            {
                var viewModel = this.InitCreateModel();
                viewModel.SetIDs(this.Decode(id.Split('/')));
                return this.Request.IsAjaxRequest() ?
                        PartialView("Create", viewModel) : (ActionResult)this.View("Create", viewModel);
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        [HttpPost, ValidateInput(true)]
        public virtual ActionResult Create([Bind(Exclude = "ID")]TViewModel viewModel)
        {
            try
            {
                if (this.ModelState.IsValid)
                {
                    viewModel = this.Repository.Create(viewModel);
                    return CreateResult(viewModel);
                }
                else
                {
                    return base.GetCreateInvalidModelResult(viewModel);
                }
            }
            catch (Exception ex)
            {
                return Error(ex, Options, viewModel);
            }
        }

        public virtual ActionResult Edit(String id)
        {
            try
            {
                var decodedIds = this.Decode(id).Single().Split('/');
                var viewModel = this.Repository.Get(decodedIds);
                if (ViewModelRetrieved != null)
                    ViewModelRetrieved(viewModel, decodedIds);

                if (Request.QueryString["Success"] == "True")
                    ViewBag.Success = true;
                return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        [HttpPost, ValidateInput(true)]
        public virtual ActionResult Edit(TViewModel viewModel)
        {
            try
            {

                if (this.ModelState.IsValid)
                {
                    viewModel = this.Repository.Update(viewModel);
                    ViewBag.Success = true;
                    return EditResult(viewModel);
                }
                else
                {
                    return base.GetEditInvalidModelResult(viewModel);
                }
            }
            catch (Exception ex)
            {
                return Error(ex, Options, viewModel);
            }
        }

        public virtual ActionResult Delete(String id)
        {
            try
            {
                var viewModel = this.Repository.Get(id.Decode().Split('/'));
                return this.Delete(viewModel);
            }
            catch (Exception ex)
            {
                return Error(ex, Options);
            }
        }

        [HttpPost]
        public virtual ActionResult Delete(TViewModel viewModel)
        {
            try
            {
                this.Repository.Delete(viewModel);
                return DeleteResult(viewModel);

            }
            catch (Exception ex)
            {
                return Error(ex, Options, viewModel);
            }
        }

        protected override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            base.OnResultExecuted(filterContext);
            this.Repository.Dispose();
        }

        protected virtual String BuildIDRoute(TViewModel viewModel)
        {
            string route = String.Empty;
            var count = 0;
            foreach (var id in viewModel.GetIDs())
            {
                if (count < viewModel.GetIDs().Count())
                    route += "/";
                route += Options.URLEncodeKey ? id.ToString().Encode() : id;
                count++;
            }

            return route;
        }

        protected virtual IEnumerable<String> Decode(params object[] ids)
        {
            foreach (var id in ids)
                yield return Options.URLEncodeKey ? id.ToString().Decode() : id.ToString();
        }

        protected virtual ActionResult CreateResult(TViewModel viewModel)
        {
            if (this.Request.IsAjaxRequest())
            {
                var filter = this.Request.QueryString["Filter"];
                if (filter.NotNull())
                    return this.RedirectToAction(Options.RedirectOnCreate ?? "Index", new { where = BuildFilterString(filter, viewModel), filter = filter });
            }

            return this.RedirectToAction(Options.RedirectOnCreate ?? "Edit", Options.PassIDOnCreateRedirect ? new { ID = BuildIDRoute(viewModel), Success = true } : null);
        }

        protected virtual ActionResult DeleteResult(TViewModel viewModel)
        {
            var filter = this.Request.QueryString["Filter"];
            if (filter.NotNull())
                return this.RedirectToAction(Options.RedirectOnDelete ?? "Index", new { where = BuildFilterString(filter, viewModel), filter = filter });

            return this.RedirectToAction(Options.RedirectOnDelete ?? "Index");
        }

        protected virtual ActionResult EditResult(TViewModel viewModel)
        {
            if (this.Request.IsAjaxRequest())
            {
                var filter = this.Request.QueryString["Filter"];
                if (filter.NotNull())
                    return this.RedirectToAction("Index", new { where = BuildFilterString(filter, viewModel), filter = filter });
                else
                    return this.RedirectToAction("Index");
            }

            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
        }

        protected virtual TViewModel InitCreateModel()
        {
            var viewModel = new TViewModel();
            var set = Convert.ToString(Request.QueryString["set"]);
            if (!String.IsNullOrEmpty(set))
            {
                var propList = set.Split(':');
                for (int i = 0; i < propList.Length; i = i + 2)
                {
                    Joe.Reflection.ReflectionHelper.SetEvalProperty(viewModel, propList[i], propList[i + 1]);
                }
            }

            viewModel = this.Repository.Default(viewModel);
            this.Repository.MapRepoFunction(viewModel, false);
            return viewModel;

        }

        private String BuildFilterString(String filter, TViewModel viewModel)
        {
            var filterProps = filter.Split(',');
            String filterString = null;
            foreach (var filterProp in filterProps)
            {
                if (filterString.NotNull())
                    filterString += ":and:" + filter + ":=:" + ReflectionHelper.GetEvalProperty(viewModel, filter).ToString();
                else
                    filterString = filter + ":=:" + ReflectionHelper.GetEvalProperty(viewModel, filter).ToString();
            }
            return filterString;
        }

    }
}
