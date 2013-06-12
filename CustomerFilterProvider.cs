using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http.Filters;
using System.Web.Http.Controllers;
using System.Web.Http;

namespace Joe.Web.Mvc
{
    public class CustomFilterProvider : IFilterProvider
    {
        public IEnumerable<FilterInfo> GetFilters(HttpConfiguration configuration, HttpActionDescriptor actionDescriptor)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("Configuration is null");
            }

            if (actionDescriptor == null)
            {
                throw new ArgumentNullException("ActionDescriptor is null");
            }

            IEnumerable<CustomFilterInfo> customActionFilters = actionDescriptor.GetFilters().Select(i => new CustomFilterInfo(i, FilterScope.Action)).Where(i => !typeof(IOrderedFilter).IsAssignableFrom(i.Instance.GetType()));
            IEnumerable<CustomFilterInfo> customControllerFilters = actionDescriptor.ControllerDescriptor.GetFilters().Select(i => new CustomFilterInfo(i, FilterScope.Controller)).Where(i => !typeof(IOrderedFilter).IsAssignableFrom(i.Instance.GetType()));

            IEnumerable<CustomFilterInfo> customActionOrderedFilters = actionDescriptor.GetFilters().Select(i => new CustomFilterInfo(i, FilterScope.Action)).Where(i => typeof(IOrderedFilter).IsAssignableFrom(i.Instance.GetType())).OrderByDescending(i => ((IOrderedFilter)i.Instance).Order);
            IEnumerable<CustomFilterInfo> customControllerOrderedFilters = actionDescriptor.ControllerDescriptor.GetFilters().Select(i => new CustomFilterInfo(i, FilterScope.Controller)).Where(i => typeof(IOrderedFilter).IsAssignableFrom(i.Instance.GetType())).OrderByDescending(i => ((IOrderedFilter)i.Instance).Order);

            var filterList = customControllerFilters.Concat(customActionFilters).Concat(customControllerOrderedFilters).Concat(customActionOrderedFilters).Select(i => i.ConvertToFilterInfo());

            return filterList;
        }

        public static void Register()
        {
            GlobalConfiguration.Configuration.Services.Add(typeof(System.Web.Http.Filters.IFilterProvider), new CustomFilterProvider());
            var providers = GlobalConfiguration.Configuration.Services.GetFilterProviders();
            var defaultprovider = providers.First(i => i is ActionDescriptorFilterProvider);
            GlobalConfiguration.Configuration.Services.Remove(typeof(System.Web.Http.Filters.IFilterProvider), defaultprovider);
        }
    }

    public class CustomFilterInfo
    {
        public CustomFilterInfo(IFilter instance, FilterScope scope)
        {
            this.Instance = instance;
            this.Scope = scope;
        }

        public IFilter Instance { get; set; }
        public FilterScope Scope { get; set; }

        public FilterInfo ConvertToFilterInfo()
        {
            return new FilterInfo(this.Instance, this.Scope);
        }
    }
}
