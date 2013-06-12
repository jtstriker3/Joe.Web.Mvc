using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using Joe.Web.Mvc;
using Joe.Map;
using System.Reflection;
using System.Net.Http;
using Joe.Business;
using Joe.MapBack;

namespace Joe.Web.Mvc
{
    [ErrorActionFilter]
    public abstract class BaseApiController<TModel, TViewModel, TRepository> : ApiController, IBusinessController
        where TModel : class, new()
        where TViewModel : class, new()
        where TRepository : class, IDBViewContext, new()
    {
        protected IBusinessObject<TModel, TViewModel, TRepository> BusinessObject { get; set; }
        public delegate TViewModel GetDelegate(TViewModel viewModel, params String[] ids);
        public delegate IQueryable<TViewModel> GetListDelegate(IQueryable<TViewModel> viewModelList);
        public GetDelegate ViewModelRetrieved;
        public GetListDelegate ViewModelListRetrieved;
        public IBusinessObject BaseBusinessObject { get { return this.BusinessObject; } }

        public BaseApiController(IBusinessObject<TModel, TViewModel, TRepository> businessObject)
        {
            BusinessObject = businessObject;
        }

        [JoeQueryable, SetCrud, MapBOFunctions]
        public virtual IQueryable<TViewModel> Get()
        {
            var viewModelList = this.BusinessObject.Get(setCrudOverride: false, mapBOFunctionsOverride: false);
            if (ViewModelListRetrieved != null)
                viewModelList = ViewModelListRetrieved(viewModelList);

            return viewModelList;
        }

        public virtual TViewModel Get(String id)
        {
            var viewModel = this.BusinessObject.Get(id);
            if (this.ViewModelRetrieved != null)
                ViewModelRetrieved(viewModel, id);
            return viewModel;
        }

        public IQueryable<TViewModel> Put(List<TViewModel> viewModelList, Boolean List)
        {
            return this.BusinessObject.Update(viewModelList);
        }

        [ValidActionFilter]
        public virtual TViewModel Put(TViewModel viewModel)
        {
            return this.BusinessObject.Update(viewModel);
        }

        [ValidActionFilter]
        public virtual TViewModel Post(TViewModel viewModel)
        {
            return this.BusinessObject.Create(viewModel);
        }

        public virtual void Delete(string id)
        {
            var viewModel = this.BusinessObject.Get(id);
            this.Delete(viewModel);
        }

        [HttpPost]
        public virtual void Delete(TViewModel viewModel)
        {
            try
            {
                this.BusinessObject.Delete(viewModel);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.BusinessObject.Dispose();
            base.Dispose(disposing);
        }

    }
}