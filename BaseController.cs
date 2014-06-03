using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Joe.Business;
using Joe.Web.Mvc;
using Joe.Web.Mvc.Utility.Configuration;
using Joe.Web.Mvc.Utility.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Validation;

namespace Joe.Web.Mvc
{
    public abstract class BaseController : Controller
    {
        public static IErrorLogProvider ErrorLogger { get; set; }

        public IRepository BaseRepository { get; set; }

        public BaseController(IRepository baseRepository)
        {
            BaseRepository = baseRepository;
            ViewBag.Success = false;
            ViewBag.Error = false;
            ViewBag.Header = String.Empty;
            ViewBag.SubHeader = String.Empty;
        }

        public TransferResult Transfer(string url)
        {
            return new TransferResult(url);
        }

        protected override IAsyncResult BeginExecute(RequestContext requestContext, AsyncCallback callback, object state)
        {
            return base.BeginExecute(requestContext, callback, state);
        }

        protected virtual IEnumerable<String> BuildErrorList()
        {
            foreach (ModelState ms in this.ModelState.Values)
            {
                foreach (ModelError error in ms.Errors)
                {
                    yield return error.ErrorMessage;
                }
            }

        }

        protected ActionResult GetAjaxErrorResponse(Exception ex, MvcOptionsAttribute options)
        {
            return this.GetAjaxErrorResponse(ex, options, null);
        }

        protected virtual ActionResult GetAjaxErrorResponse(Exception ex, MvcOptionsAttribute options, Object viewModel)
        {
            LogError(ex);
            ViewBag.Error = true;
            ViewBag.ErrorText = this.GetErrorMessage(ex, options);
            Response.StatusCode = 500;
            if (ConfigurationHelper.Debug)
                return Json(new { errors = ViewBag.ErrorText, stackTrace = ex.StackTrace }, JsonRequestBehavior.AllowGet);
            else
                return Json(new { errors = ViewBag.ErrorText }, JsonRequestBehavior.AllowGet);

        }

        protected ActionResult GetErrorResponse(Exception ex, MvcOptionsAttribute options)
        {
            return this.GetErrorResponse(ex, options, null);
        }

        protected virtual ActionResult GetErrorResponse(Exception ex, MvcOptionsAttribute options, Object viewModel)
        {
            if (ex is ValidationException || ex is DbUnexpectedValidationException)
            {
                var validationEx = ex as ValidationException;
                this.ModelState.AddModelError(String.Empty, ex.Message);
            }
            else if (ex is DbEntityValidationException)
            {
                var exception = ex as DbEntityValidationException;
                foreach (var error in exception.EntityValidationErrors.SelectMany(ev => ev.ValidationErrors.Select(ve => ve)))
                {
                    this.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }

                this.ModelState.AddModelError(String.Empty, ex.Message);
            }
            else
            {
                LogError(ex);
                if (ConfigurationHelper.Debug)
                    throw ex;
                else
                {
                    ViewBag.Error = true;
                    Response.StatusCode = 500;
                    ViewBag.ErrorText = this.GetErrorMessage(ex, options);
                }

                if (options.ReturnErrorViewOnError)
                {
                    Session["ErrorText"] = ViewBag.ErrorText;
                    return Redirect(ConfigurationHelper.DefaultErrorpage);
                }
            }

            if (viewModel.NotNull())
                this.BaseRepository.MapRepoFunction(viewModel);

            return (ActionResult)this.View(viewModel);

        }

        private IEnumerable<String> GetErrorMessage(Exception ex, MvcOptionsAttribute options)
        {
            List<String> errorMessage = new List<string>();
            switch (this.RouteData.Values["Action"].ToString().ToLower())
            {
                case "edit":
                    if (options.NotNull() && options.EditErrorMessage.NotNull())
                        errorMessage.Add(options.EditErrorMessage);
                    else
                        errorMessage.AddRange(ex.BuildExceptionsMessageString());
                    break;
                case "create":
                    if (options.NotNull() && options.CreateErrorMessage.NotNull())
                        errorMessage.Add(options.CreateErrorMessage);
                    else
                        errorMessage.AddRange(ex.BuildExceptionsMessageString());
                    break;
                case "delete":
                    if (options.NotNull() && options.DeleteErrorMessage.NotNull())
                        errorMessage.Add(options.DeleteErrorMessage);
                    else
                        errorMessage.AddRange(ex.BuildExceptionsMessageString());
                    break;
                case "index":
                    if (options.NotNull() && options.IndexErrorMessage.NotNull())
                        errorMessage.Add(options.IndexErrorMessage);
                    else
                        errorMessage.AddRange(ex.BuildExceptionsMessageString());
                    break;
                default:
                    errorMessage.AddRange(ex.BuildExceptionsMessageString());
                    break;
            }
            return errorMessage;
        }

        protected virtual ActionResult GetCreateInvalidModelResult(Object viewModel)
        {
            ViewBag.ErrorText = BuildErrorList();
            ViewBag.Error = true;
            Response.StatusCode = 422;
            this.BaseRepository.MapRepoFunction(viewModel, false);
            return Request.IsAjaxRequest() ? Json(new { errors = BuildErrorList() }, JsonRequestBehavior.AllowGet) : (ActionResult)this.View(viewModel);
        }

        protected virtual ActionResult GetEditInvalidModelResult(Object viewModel)
        {
            ViewBag.ErrorText = BuildErrorList();
            ViewBag.Error = true;
            Response.StatusCode = 422;
            this.BaseRepository.MapRepoFunction(viewModel, true);
            return Request.IsAjaxRequest() ? Json(new { errors = BuildErrorList() }, JsonRequestBehavior.AllowGet) : (ActionResult)this.View(viewModel);
        }

        protected virtual void LogError(Exception ex)
        {
            try
            {
                if (ErrorLogger.NotNull())
                    ErrorLogger.LogError(ex);
            }
            catch
            {
                //Do Nothing, Error Could not be logged
            }
        }

        protected ActionResult Error(Exception ex, MvcOptionsAttribute options = null, Object viewModel = null)
        {
            return Request.IsAjaxRequest() ? GetAjaxErrorResponse(ex, options, viewModel) : GetErrorResponse(ex, options, viewModel);
        }
    }
}