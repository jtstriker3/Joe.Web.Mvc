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
using System.Web.Http.OData.Query;

namespace Joe.Web.Mvc
{
    [ErrorActionFilter]
    public abstract class BaseApiController<TModel, TViewModel, TRepository> : ApiController, IBusinessController
        where TModel : class, new()
        where TViewModel : class, new()
        where TRepository : class, IDBViewContext, new()
    {
        protected IRepository<TModel, TViewModel, TRepository> Repository { get; set; }
        public delegate TViewModel GetDelegate(TViewModel viewModel, params String[] ids);
        public delegate IQueryable<TViewModel> GetListDelegate(IQueryable<TViewModel> viewModelList);
        public GetDelegate ViewModelRetrieved;
        public GetListDelegate ViewModelListRetrieved;
        public IRepository BaseRepository { get { return this.Repository; } }

        public BaseApiController(IRepository<TModel, TViewModel, TRepository> businessObject)
        {
            Repository = businessObject;
        }

        [JoeQueryable(
            AllowedQueryOptions = AllowedQueryOptions.All,
            AllowedArithmeticOperators = AllowedArithmeticOperators.All,
            AllowedLogicalOperators = AllowedLogicalOperators.All,
            AllowedFunctions = AllowedFunctions.All), SetCrud, MapRepoFunctions]
        public virtual IQueryable<TViewModel> Get()
        {
            try
            {
                var viewModelList = this.Repository.Get(setCrudOverride: false, mapRepoFunctionsOverride: false);
                if (ViewModelListRetrieved != null)
                    viewModelList = ViewModelListRetrieved(viewModelList);

                return viewModelList;
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                throw ex;
            }
        }

        public virtual TViewModel Get(String id)
        {
            try
            {
                var viewModel = this.Repository.Get(id);
                if (this.ViewModelRetrieved != null)
                    ViewModelRetrieved(viewModel, id);
                return viewModel;
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                throw ex;
            }
        }

        public IQueryable<TViewModel> Put(List<TViewModel> viewModelList, Boolean List)
        {
            try
            {
                var results = this.Repository.Update(viewModelList);
                return results.Select(result => result.ViewModel).AsQueryable();
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                throw ex;
            }
        }

        [ValidActionFilter]
        public virtual TViewModel Put(TViewModel viewModel)
        {
            try
            {
                var result = this.Repository.Update(viewModel);
                return result.ViewModel;
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                throw ex;
            }
        }

        [ValidActionFilter]
        public virtual TViewModel Post(TViewModel viewModel)
        {
            try
            {
                var result = this.Repository.Create(viewModel);
                return result.ViewModel;
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                throw ex;
            }
        }

        public virtual void Delete(string id)
        {
            var viewModel = this.Repository.Get(id);
            this.Delete(viewModel);
        }

        [HttpDelete]
        public virtual void Delete(TViewModel viewModel)
        {
            try
            {
                this.Repository.Delete(viewModel);
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                throw ex;
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.Repository.Dispose();
            base.Dispose(disposing);
        }

        protected void LogError(Exception ex)
        {
            if (ErrorLogger.LogProvider != null)
                ErrorLogger.LogProvider.LogError(ex);
        }

    }
}