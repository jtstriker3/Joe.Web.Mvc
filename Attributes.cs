using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net.Http;
using System.Web.Http.Controllers;
using Newtonsoft.Json;
using System.Collections;
using System.Reflection;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.Serialization;
using Joe.Reflection;
using Joe.Web.Mvc.Utility.Extensions;


namespace Joe.Web.Mvc
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class CrudSecurity : ActionFilterAttribute
    {
        public CrudOperation CrudOperation { get; set; }

        /// <summary>
        /// Use this if applying to a specific Action
        /// </summary>
        /// <param name="operation"></param>
        public CrudSecurity(CrudOperation operation)
        {
            CrudOperation = operation;
        }

        /// <summary>
        /// Use this if applying to a class
        /// </summary>
        public CrudSecurity()
        {

        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var result = filterContext.Result as ViewResultBase;
            if (CrudOperation == CrudOperation.NotSet)
            {
                switch (filterContext.RequestContext.RouteData.Values["Action"].ToString().ToLower())
                {
                    case "create":
                        CrudOperation = Mvc.CrudOperation.Create;
                        break;
                    case "details":
                        CrudOperation = Mvc.CrudOperation.Delete;
                        break;
                    case "edit":
                        CrudOperation = Mvc.CrudOperation.Update;
                        break;
                    case "delete":
                        CrudOperation = Mvc.CrudOperation.Delete;
                        break;
                }
            }
            if (result != null)
            {
                var model = result.Model;
                if (model.HasCrudProerties())
                    switch (CrudOperation)
                    {
                        case CrudOperation.Create:
                            if (!(Boolean)ReflectionHelper.GetEvalProperty(model, "CanCreate"))
                            {
                                filterContext.HttpContext.Response.StatusCode = 403;
                                filterContext.HttpContext.Response.Flush();
                                filterContext.HttpContext.Response.End();
                            }

                            break;
                        case CrudOperation.Read:
                            if (!(Boolean)ReflectionHelper.GetEvalProperty(model, "CanRead"))
                            {
                                filterContext.HttpContext.Response.StatusCode = 403;
                                filterContext.HttpContext.Response.Flush();
                                filterContext.HttpContext.Response.End();
                            }
                            break;
                        case CrudOperation.Update:
                            if (!(Boolean)ReflectionHelper.GetEvalProperty(model, "CanUpdate"))
                            {
                                filterContext.HttpContext.Response.StatusCode = 403;
                                filterContext.HttpContext.Response.Flush();
                                filterContext.HttpContext.Response.End();
                            }
                            break;
                        case CrudOperation.Delete:
                            if (!(Boolean)ReflectionHelper.GetEvalProperty(model, "CanDelete"))
                            {
                                filterContext.HttpContext.Response.StatusCode = 403;
                                filterContext.HttpContext.Response.Flush();
                                filterContext.HttpContext.Response.End();
                            }
                            break;
                    }
            }
        }
    }

    public enum CrudOperation
    {
        NotSet = 0,
        Create = 1,
        Read = 2,
        Update = 3,
        Delete = 4
    }
}

