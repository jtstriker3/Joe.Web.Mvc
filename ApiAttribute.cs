using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http.Controllers;
using Joe.Web.Mvc.Utility.Extensions;
using System.Collections;
using Joe.Reflection;

namespace Joe.Web.Mvc
{
    public class ApiAttribute : Attribute
    {
        public Boolean SetCrud { get; set; }
        public Boolean MapRepoFunctions { get; set; }

        public ApiAttribute()
        {
            SetCrud = true;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public class ValidActionFilter : System.Web.Http.Filters.ActionFilterAttribute, IOrderedFilter
    {
        public int Order { get; private set; }

        public ValidActionFilter()
        {
            Order = -1;
        }

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var modelState = actionContext.ModelState;
            if (!modelState.IsValid)
            {
                var errors = modelState.Select(model => model.Value.Errors.FirstOrDefault().ErrorMessage);
                actionContext.Response =
                    new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.BadRequest, new { errors = errors });
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public class ErrorActionFilter : System.Web.Http.Filters.ExceptionFilterAttribute
    {
        public override void OnException(System.Web.Http.Filters.HttpActionExecutedContext actionExecutedContext)
        {
            actionExecutedContext.Exception.LogError();
            actionExecutedContext.SetErrorResponse(actionExecutedContext.Exception);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class SetCrudAttribute : System.Web.Http.Filters.ActionFilterAttribute, IOrderedFilter
    {
        public int Order { get; private set; }

        public SetCrudAttribute()
        {
            Order = int.MaxValue;
        }

        public override void OnActionExecuted(System.Web.Http.Filters.HttpActionExecutedContext actionExecutedContext)
        {
            try
            {
                if (actionExecutedContext.Response.IsSuccessStatusCode)
                {
                    var controller = actionExecutedContext.ActionContext.ControllerContext.Controller;
                    var value = ((ObjectContent)actionExecutedContext.Response.Content).Value;
                    Type genericType = null;
                    if (value.GetType().IsGenericType)
                        genericType = value.GetType().GetGenericArguments().Single();
                    var SetCrud = ((ApiAttribute)controller.GetType().GetCustomAttributes(typeof(ApiAttribute), true).SingleOrDefault() ?? new ApiAttribute()).SetCrud;
                    IEnumerable<String> totalCountList;
                    String count = null;
                    if (actionExecutedContext.Response.Headers.TryGetValues("X-Total-Count", out totalCountList))
                        count = totalCountList.FirstOrDefault();
                    if (SetCrud)
                    {
                        if (typeof(IBusinessController).IsAssignableFrom(controller.GetType()))
                        {
                            if (typeof(IEnumerable).IsAssignableFrom(value.GetType()))
                            {
                                Func<Object, Boolean> filter = viewModel =>
                                        {
                                            var propertyInfo = ReflectionHelper.TryGetEvalPropertyInfo(viewModel.GetType(), "CanRead");
                                            if (propertyInfo.NotNull())
                                                return ((Boolean)ReflectionHelper.GetEvalProperty(viewModel, "CanRead"));
                                            return true;
                                        };
                                var viewModelList = ((IEnumerable)value).Cast<Object>().ToList();
                                if (viewModelList.GetType().IsGenericType && value.GetType().GetGenericArguments().Count() > 1 && typeof(Grouping<,>).IsAssignableFrom(value.GetType().GetGenericArguments()[1].GetGenericTypeDefinition()))
                                {
                                    foreach (var group in viewModelList)
                                    {
                                        var elements = ((IEnumerable)ReflectionHelper.GetEvalProperty(group, "Elements")).Cast<Object>();
                                        foreach (var viewModel in elements)
                                            ((IBusinessController)controller).BaseRepository.SetCrud(viewModel, true);

                                        elements = elements.Where(filter).AsQueryable();
                                    }

                                    actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.OK, viewModelList.TryCast(genericType));
                                }
                                else
                                {
                                    foreach (var viewModel in viewModelList)
                                        ((IBusinessController)controller).BaseRepository.SetCrud(viewModel, true);

                                    ((ObjectContent)actionExecutedContext.Response.Content).Value = viewModelList.Where(filter).TryCast(genericType);
                                }

                                actionExecutedContext.Response.Headers.Add("X-Total-Count", count);
                            }
                            else
                                throw new Exception("Content value must be IEnumerable");
                        }
                        else
                            throw new Exception("SetCrud must apply to Controller that implements from Joe.Web.Mvc.IBusinessController");
                    }
                }
            }
            catch (Exception ex)
            {
                ex.LogError();
                actionExecutedContext.SetErrorResponse(ex);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class MapRepoFunctions : System.Web.Http.Filters.ActionFilterAttribute, IOrderedFilter
    {
        public int Order { get; private set; }

        public MapRepoFunctions()
        {
            Order = int.MaxValue - 1;
        }

        public override void OnActionExecuted(System.Web.Http.Filters.HttpActionExecutedContext actionExecutedContext)
        {
            try
            {
                if (actionExecutedContext.Response.IsSuccessStatusCode)
                {
                    var controller = actionExecutedContext.ActionContext.ControllerContext.Controller;
                    var value = ((ObjectContent)actionExecutedContext.Response.Content).Value;
                    Type genericType = null;
                    if (value.GetType().IsGenericType)
                        genericType = value.GetType().GetGenericArguments().Single();
                    var mapRepoFuntions = ((ApiAttribute)controller.GetType().GetCustomAttributes(typeof(ApiAttribute), true).SingleOrDefault() ?? new ApiAttribute()).MapRepoFunctions;
                    IEnumerable<String> totalCountList;
                    String count = null;
                    if (actionExecutedContext.Response.Headers.TryGetValues("X-Total-Count", out totalCountList))
                        count = totalCountList.FirstOrDefault();
                    if (mapRepoFuntions)
                    {
                        if (typeof(IBusinessController).IsAssignableFrom(controller.GetType()))
                        {
                            if (typeof(IEnumerable).IsAssignableFrom(value.GetType()))
                            {
                                var viewModelList = ((IEnumerable)value).Cast<Object>().ToList();
                                if (viewModelList.GetType().IsGenericType && value.GetType().GetGenericArguments().Count() > 1 && typeof(Grouping<,>).IsAssignableFrom(value.GetType().GetGenericArguments()[1].GetGenericTypeDefinition()))
                                {
                                    foreach (var group in viewModelList)
                                    {
                                        var elements = ((IEnumerable)ReflectionHelper.GetEvalProperty(group, "Elements")).Cast<Object>();
                                        foreach (var viewModel in elements)
                                            ((IBusinessController)controller).BaseRepository.MapRepoFunction(viewModel);
                                    }
                                    actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.OK, viewModelList.TryCast(genericType));
                                }
                                else
                                {
                                    foreach (var viewModel in viewModelList)
                                        ((IBusinessController)controller).BaseRepository.MapRepoFunction(viewModel);
                                    ((ObjectContent)actionExecutedContext.Response.Content).Value = viewModelList.TryCast(genericType);
                                }

                                actionExecutedContext.Response.Headers.Add("X-Total-Count", count);
                            }
                            else
                                throw new Exception("Content value must be IEnumerable");
                        }
                        else
                            throw new Exception("MapRepoFunctions must apply to Controller that implements from Joe.Web.Mvc.IBusinessController");
                    }
                }
            }
            catch (Exception ex)
            {
                ex.LogError();
                actionExecutedContext.SetErrorResponse(ex);
            }
        }
    }

}
