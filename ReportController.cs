using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Joe.Business.Report;
using DoddleReport;
using DoddleReport.Web;
using Joe.Web.Mvc.Utility.Extensions;
using Joe.Map;

namespace Joe.Web.Mvc
{
    public abstract class ReportController<TRepository> : Controller
    {
        //
        // GET: /Report/

        public ActionResult Index()
        {
            ReportRepository reportRepo = new ReportRepository();
            return this.Request.IsAjaxRequest() ? PartialView(reportRepo.GetReports()) : (ActionResult)View(reportRepo.GetReports());
        }

        public ActionResult Filters(String id)
        {
            IReportRepository reportRepo = new ReportRepository();
            var report = reportRepo.GetReport(id);
            if (report.Single)
                report.SingleChoices = reportRepo.GetSingleList<TRepository>(report);
            foreach (var filter in report.Filters)
                if (filter.IsListFilter)
                    filter.ListValues = reportRepo.GetFilterValues<TRepository>(filter);
            return this.Request.IsAjaxRequest() ? PartialView(report) : (ActionResult)View(report);
        }

        public ActionResult Run([Bind(Exclude = "ReportFilterAttribute")] Joe.Business.Report.Report report)
        {
            IReportRepository reportRepo = new ReportRepository();

            var result = reportRepo.Run<TRepository>(report);
            var extension = this.Request.RequestContext.RouteData.Values["extension"];
            var isNotHtml = extension != null && !extension.ToString().ToLower().Contains("html");
            var reportFromView = reportRepo.GetReport(report.Name);

            if (report.UiHint.NotNull())
            {
                ViewBag.Title = report.Name;
                ViewBag.Description = report.Description;
                ViewBag.Filters = report.Filters.BuildFilterHeading();
                if (typeof(IEnumerable).IsAssignableFrom(result.GetType()))
                {
                    var ienumerableResult = (IEnumerable)result;
                    var where = this.HttpContext.Request.QueryString["where"];
                    if (where.NotNull())
                        ienumerableResult = ienumerableResult.Filter(where);

                    return View(report.UiHint, ienumerableResult);
                }

                return View(report.UiHint, result);
            }
            else
            {
                DoddleReport.Report doddleReport = new DoddleReport.Report();
                if (!isNotHtml)
                    doddleReport.TextFields.Title = "<link href='/content/report.css' rel='stylesheet' />" + reportFromView.Name;
                else
                    doddleReport.TextFields.Title = reportFromView.Name;
                doddleReport.TextFields.SubTitle = reportFromView.Description;
                if (typeof(IEnumerable).IsAssignableFrom(result.GetType()))
                {
                    var ienumerableResult = (IEnumerable)result;
                    var where = this.HttpContext.Request.QueryString["where"];
                    if (where.NotNull())
                        ienumerableResult = ienumerableResult.Filter(where);
                    if (!isNotHtml && (!report.Filters.NotNull() || report.Filters.Count() == 0))
                        doddleReport.TextFields.Header = "&nbsp;";

                    doddleReport.Source = ienumerableResult.ToReportSource();
                }
                else
                {
                    var hasIenumerableProperty = result.GetType().GetProperties().Where(prop => prop.PropertyType.ImplementsIEnumerable()).Count() > 0;
                    var reportList = result.UnionAllList();
                    if (!hasIenumerableProperty)
                        reportList = new List<Object>() { result };

                    doddleReport.TextFields.Header = result.BuildReportHeading(report.Filters);
                    doddleReport.Source = reportList.ToReportSource();

                    //foreach (var childReport in result.BuildChildReports())
                    //{
                    //    doddleReport.AppendReport(childReport);
                    //}

                }

                doddleReport.TextFields.Header += Environment.NewLine + "<b>Filters</b><br/>" + report.Filters.BuildFilterHeading();
                if (reportFromView.Filters.NotNull())
                    foreach (var filter in reportFromView.Filters)
                        if (!filter.IsListFilter)
                        {
                            var field = doddleReport.DataFields[filter.PropertyName];
                            var useFilterField = doddleReport.DataFields[filter.PropertyName + "Active"];
                            if (field.NotNull())
                                field.Hidden = true;
                            if (useFilterField.NotNull())
                                useFilterField.Hidden = true;
                        }
                var idField = doddleReport.DataFields["ID"]
                    ?? doddleReport.DataFields["Id"]
                    ?? doddleReport.DataFields["id"];
                if (idField.NotNull())
                    idField.Hidden = true;

                return new ReportResult(doddleReport);
            }
        }

    }
}
