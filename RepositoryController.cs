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
using System.ComponentModel;

namespace Joe.Web.Mvc
{
    public abstract class RepositoryController<TModel, TViewModel> : BaseController
        where TModel : class
        where TViewModel : class, new()
    {
        protected MvcOptionsAttribute Options { get; set; }
        public IRepository<TModel, TViewModel> Repository { get; set; }
        public delegate TViewModel GetDelegate(TViewModel viewModel, params String[] ids);
        public delegate IQueryable<TViewModel> GetListDelegate(IQueryable<TViewModel> viewModelList);
        public GetDelegate ViewModelRetrieved;
        public GetListDelegate ViewModelListRetrived;


        public RepositoryController(IRepository<TModel, TViewModel> repository)
            : base(repository)
        {
            Options = (MvcOptionsAttribute)GetType().GetCustomAttributes(typeof(MvcOptionsAttribute), true).SingleOrDefault() ?? new MvcOptionsAttribute();

            ViewBag.TrueDelete = Options.TrueDelete;
            ViewBag.ShowSubMenu = Options.ShowSubMenu;
            ViewBag.ShowCreate = Options.ShowCreate;
            ViewBag.Create = false;

            Repository = repository;
        }

        public virtual ActionResult Index()
        {
            return this.Index(String.Empty);
        }

        [HttpPost]
        public virtual ActionResult Index(String filter)
        {
            string filterString = null;
            try
            {
                if (!String.IsNullOrWhiteSpace(filter))
                {
                    var requestProperties = this.Request.QueryString["filterProperties"];
                    var filterProperties = requestProperties.NotNull() ? requestProperties.Split(',') : Options.FilterProperties;
                    foreach (var filterProp in filterProperties)
                    {
                        foreach (var search in filter.Split('|'))
                        {
                            var propertyType = typeof(TViewModel).GetProperty(filterProp).PropertyType;
                            var operation = propertyType.IsValueType ? ":=:" : ":Contains:";

                            Decimal outParse;
                            var canParse = decimal.TryParse(search.Trim(), out outParse);

                            if (!propertyType.IsValueType || canParse)
                            {
                                if (!filterString.NotNull())
                                    filterString = filterProp + operation + search.Trim();
                                else
                                    filterString += ":or:" + filterProp + operation + search.Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error Generating Filter String. Please Make sure All Properties sepcified in MVCOptions are Valid", ex);
            }
            return Index(null, filterString: filterString);
        }

        internal virtual ActionResult Index(Expression<Func<TViewModel, Boolean>> filter, Object dynamicFilters = null, String filterString = null)
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
                    take = Request.QueryString["take"].ToNullable<int>();
                    skip = filterString.NotNull() ? null : Request.QueryString["skip"].ToNullable<int>();
                    orderBy = Convert.ToString(Request.QueryString["orderby"]);
                    descending = Convert.ToBoolean(Request.QueryString["descending"]);
                    where = filterString ?? Convert.ToString(Request.QueryString["where"]);
                    int count;
                    var viewModelList = this.Repository.Get(out count, filter, null, take.HasValue ? take : Options.DefaultPageSize, skip, descending: descending, orderBy: orderBy, stringFilter: where, dynamicFilter: dynamicFilters, mapRepoFunctionsOverride: this.Options.MapRepoFunctionForList);
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
            var decodeIds = id.Split('/');
            decodeIds = this.Decode(decodeIds).ToArray();
            try
            {
                viewModel = this.Repository.Get(decodeIds);
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
            ViewBag.Create = true;
            var viewModel = this.InitCreateModel();
            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
        }

        [ActionName("CreateWithID")]
        public virtual ActionResult Create(string id)
        {
            try
            {
                ViewBag.Create = true;
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
                ViewBag.Create = true;
                if (this.ModelState.IsValid)
                {

                    var result = this.Repository.Create(viewModel);
                    viewModel = result.ViewModel;
                    this.AddWarningsToModelState(result.Warnings);
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
                return this.GetEditResult(viewModel);
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
                    var result = this.Repository.Update(viewModel);
                    viewModel = result.ViewModel;
                    ViewBag.Success = true;
                    if (Options.ClearModelState)
                        this.ModelState.Clear();
                    //Must Be done after clearing of model state
                    this.AddWarningsToModelState(result.Warnings);
                    return EditResult(viewModel);
                }
                else
                {
                    return base.GetEditInvalidModelResult(viewModel);
                }
            }
            catch (Exception ex)
            {
                TViewModel errorModel;
                if (this.TryGetModelOnError(out errorModel, viewModel.GetIDs().ToArray()))
                    viewModel = errorModel;
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

        internal virtual String BuildIDRoute(TViewModel viewModel)
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

        internal virtual IEnumerable<String> Decode(params object[] ids)
        {
            foreach (var id in ids)
                yield return Options.URLEncodeKey ? id.ToString().Decode() : id.ToString();
        }

        internal virtual ActionResult CreateResult(TViewModel viewModel)
        {
            if (this.Request.IsAjaxRequest())
            {
                var filter = this.Request.QueryString["Filter"];
                if (filter.NotNull())
                    return this.RedirectToAction(Options.RedirectOnCreate ?? "Index", new { where = HelperExtentions.BuildFilterString(filter, viewModel), filter = filter });
            }

            return this.RedirectToAction(Options.RedirectOnCreate ?? "Edit", Options.PassIDOnCreateRedirect ? new { ID = BuildIDRoute(viewModel), Success = true } : null);
        }

        internal virtual ActionResult DeleteResult(TViewModel viewModel)
        {
            var filter = this.Request.QueryString["Filter"];
            if (filter.NotNull())
                return this.RedirectToAction(Options.RedirectOnDelete ?? "Index", new { where = HelperExtentions.BuildFilterString(filter, viewModel), filter = filter });

            return this.RedirectToAction(Options.RedirectOnDelete ?? "Index");
        }

        internal virtual ActionResult EditResult(TViewModel viewModel)
        {
            if (this.Request.IsAjaxRequest())
            {
                var filter = this.Request.QueryString["Filter"];
                if (filter.NotNull())
                    return this.RedirectToAction("Index", new { where = HelperExtentions.BuildFilterString(filter, viewModel), filter = filter });
                else
                    return this.RedirectToAction("Index");
            }

            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
        }

        internal virtual ActionResult GetEditResult(TViewModel viewModel)
        {
            return Request.IsAjaxRequest() ? PartialView(viewModel) : (ActionResult)this.View(viewModel);
        }

        internal virtual TViewModel InitCreateModel()
        {
            var viewModel = new TViewModel();
            SetValuesFromQueryString(viewModel);

            viewModel = this.Repository.Default(viewModel);
            this.Repository.MapRepoFunction(viewModel, false);
            return viewModel;

        }

        internal void SetValuesFromQueryString(TViewModel viewModel)
        {
            var set = Convert.ToString(Request.QueryString["set"]);
            if (!String.IsNullOrEmpty(set))
            {
                var propList = set.Split(':');
                for (int i = 0; i < propList.Length; i = i + 2)
                {
                    var info = Joe.Reflection.ReflectionHelper.GetEvalPropertyInfo(typeof(TViewModel), propList[i]);
                    Object value = null;
                    if (typeof(int?).IsAssignableFrom(info.PropertyType))
                        value = int.Parse(propList[i + 1]);
                    else if (info.PropertyType.IsEnum)
                        value = Enum.Parse(info.PropertyType, propList[i + 1]);
                    else
                        value = propList[i + 1];
                    Joe.Reflection.ReflectionHelper.SetEvalProperty(viewModel, propList[i], value);
                }
            }
        }

        internal virtual Boolean TryGetModelOnError(out TViewModel viewModel, params Object[] ids)
        {
            Boolean success = false;
            viewModel = null;
            try
            {
                viewModel = this.Repository.Get(ids);
                success = true;
            }
            catch
            {
                success = false;
            }

            return success;
        }

    }

    class RepositoryControllerWrapper<TModel, TViewModel> : RepositoryController<TModel, TViewModel>
        where TModel : class
        where TViewModel : class, new()
    {
        public RepositoryControllerWrapper(IRepository<TModel, TViewModel> repository) : base(repository) { }
    }

    public abstract class RepositoryController<TModel, TViewModel, TListViewModel> : Controller
        where TModel : class
        where TViewModel : class, new()
        where TListViewModel : class, new()
    {
        protected Type _repositoryType;

        private RepositoryController<TModel, TViewModel> _fullRepositoryController;
        protected RepositoryController<TModel, TViewModel> FullRepositoryController
        {
            get
            {
                _fullRepositoryController = _fullRepositoryController ?? this.CreateController<TViewModel>((IRepository<TModel, TViewModel>)Joe.Business.Repository.CreateRepo(_repositoryType, typeof(TModel), typeof(TViewModel)));
                return _fullRepositoryController;
            }
        }

        private RepositoryController<TModel, TListViewModel> _listRepositoryController;
        protected RepositoryController<TModel, TListViewModel> ListRepositoryController
        {
            get
            {
                _listRepositoryController = _listRepositoryController ?? this.CreateController<TListViewModel>((IRepository<TModel, TListViewModel>)Joe.Business.Repository.CreateRepo(_repositoryType, typeof(TModel), typeof(TListViewModel)));
                return _listRepositoryController;
            }
        }

        protected RepositoryController(Type repositoryType) : base()
        {
            _repositoryType = repositoryType;
        }

        public virtual ActionResult Index()
        {
            return ListRepositoryController.Index();
        }

        [HttpPost]
        public virtual ActionResult Index(String filter)
        {
            return ListRepositoryController.Index(filter);
        }

        protected virtual ActionResult Index(Expression<Func<TListViewModel, Boolean>> filter, Object dynamicFilters = null, String filterString = null)
        {
            return ListRepositoryController.Index(filter, dynamicFilters, filterString);
        }

        public virtual ActionResult Details(String id)
        {
            return FullRepositoryController.Details(id);
        }

        public virtual ActionResult Create()
        {
            return FullRepositoryController.Create();
        }

        [ActionName("CreateWithID")]
        public virtual ActionResult Create(string id)
        {
            return FullRepositoryController.Create(id);
        }

        [HttpPost, ValidateInput(true)]
        public virtual ActionResult Create([Bind(Exclude = "ID")]TViewModel viewModel)
        {
            return FullRepositoryController.Create(viewModel);
        }

        public virtual ActionResult Edit(String id)
        {
            return FullRepositoryController.Edit(id);
        }

        [HttpPost, ValidateInput(true)]
        public virtual ActionResult Edit(TViewModel viewModel)
        {
            return FullRepositoryController.Edit(viewModel);
        }

        public virtual ActionResult Delete(String id)
        {
            return FullRepositoryController.Delete(id);
        }

        [HttpPost]
        public virtual ActionResult Delete(TViewModel viewModel)
        {
            return FullRepositoryController.Delete(viewModel);
        }

        protected virtual RepositoryController<TModel, TView> CreateController<TView>(IRepository<TModel, TView> repository)
             where TView : class, new()
        {
            var controller = new RepositoryControllerWrapper<TModel, TView>(repository);
            controller.ControllerContext = new ControllerContext(this.HttpContext, this.RouteData, this);
            return controller;
        }
    }
}
