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
            ReportRepository reportBO = new ReportRepository();
            return this.Request.IsAjaxRequest() ? PartialView(reportBO.GetReports()) : (ActionResult)View(reportBO.GetReports());
        }

        public ActionResult Filters(String id)
        {
            IReportRepository reportBO = new ReportRepository();
            var report = reportBO.GetReport(id);
            if (report.Single)
                report.SingleChoices = reportBO.GetSingleList<TRepository>(report);
            foreach (var filter in report.Filters)
                if (filter.IsListFilter)
                    filter.ListValues = reportBO.GetFilterValues<TRepository>(filter);
            return this.Request.IsAjaxRequest() ? PartialView(report) : (ActionResult)View(report);
        }

        public ReportResult Run([Bind(Exclude = "ReportFilterAttribute")] Joe.Business.Report.Report report)
        {
            IReportRepository reportBO = new ReportRepository();

            var result = reportBO.Run<TRepository>(report);
            var extension = this.Request.RequestContext.RouteData.Values["extension"];
            var isNotHtml = extension != null && !extension.ToString().ToLower().Contains("html");
            var reportFromView = reportBO.GetReport(report.Name);
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
                if (!isNotHtml && report.Filters.Count() == 0)
                    doddleReport.TextFields.Header = "&nbsp;";
                doddleReport.Source = ienumerableResult.ToReportSource();
            }
            else
            {
                var hasIenumerableProperty = result.GetType().GetProperties().Where(prop => prop.PropertyType.ImplementsIEnumerable()).Count() > 0;
                var reportList = result.UnionAllList();
                if (!hasIenumerableProperty)
                    reportList = new List<Object>() { result };
                else
                    doddleReport.TextFields.Header = result.BuildReportHeading(report.Filters);
                doddleReport.Source = reportList.ToReportSource();

                //foreach (var childReport in result.BuildChildReports())
                //{
                //    doddleReport.AppendReport(childReport);
                //}

            }

            doddleReport.TextFields.Header += Environment.NewLine + report.Filters.BuildFilterHeading();
            if (reportFromView.Filters.NotNull())
                foreach (var filter in reportFromView.Filters)
                    if (!filter.IsListFilter)
                    {
                        var field = doddleReport.DataFields[filter.PropertyName];
                        if (field.NotNull())
                            field.Hidden = true;
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
