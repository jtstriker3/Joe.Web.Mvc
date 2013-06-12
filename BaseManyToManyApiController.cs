using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using Joe.Map;
using System.Reflection;
using System.Net.Http;
using Joe.Web.Mvc;
using Joe.Business;
using Joe.MapBack;


namespace Joe.Web.Mvc
{
    public abstract class BaseManyToManyApiController<TModel, TViewModel, TRepository>
        : BaseApiController<TModel, TViewModel, TRepository>
        where TModel : class, new()
        where TViewModel : class, IManyToMany, new()
        where TRepository : class, IDBViewContext, new()
    {

        public BaseManyToManyApiController(IBusinessObject<TModel, TViewModel, TRepository> businessObject)
            : base(businessObject)
        {
        }

        public override IQueryable<TViewModel> Get()
        {
            var indexBy = Request.RequestUri.ParseQueryString()["indexBy"];
            var filterID = Request.RequestUri.ParseQueryString()["filterID"];
            switch (indexBy)
            {
                case "1":
                    return base.Get().Where(viewModel => viewModel.ID1 == (int)Convert.ChangeType(filterID, viewModel.ID1.GetType()));
                case "2":
                    return base.Get().Where(viewModel => viewModel.ID2 == (int)Convert.ChangeType(filterID, viewModel.ID2.GetType()));
                default:
                    throw new Exception("You must include an indexBy Parameter in the query string");

            }
        }

        public virtual TViewModel Get(String id1, String id2)
        {
            var viewModel = this.BusinessObject.Get(id1, id2);
            if (this.ViewModelRetrieved != null)
                ViewModelRetrieved(viewModel, id1, id2);
            return viewModel;
        }

        public virtual void Delete(String id, String id2, String id3)
        {
            var viewModel = this.BusinessObject.Get(id, id2, id3);
            this.Delete(viewModel);
        }

        public virtual void Delete(String id, String id2)
        {
            var viewModel = this.BusinessObject.Get(id, id2);
            this.Delete(viewModel);
        }

    }
}
