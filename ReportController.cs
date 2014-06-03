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
using System.ComponentModel.DataAnnotations;
using Joe.MapBack;
using DotNet.Highcharts.Options;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Enums;

namespace Joe.Web.Mvc
{
    public abstract class ReportController<TRepository> : Controller
        where TRepository : IDBViewContext, new()
    {
        //
        // GET: /Report/

        public ActionResult Index()
        {
            ReportRepository reportRepo = new ReportRepository();
            return this.Request.IsAjaxRequest() ? PartialView(FilterReports(reportRepo.GetReports())) : (ActionResult)View(FilterReports(reportRepo.GetReports()));
        }

        public ActionResult Filters(String id)
        {
            IReportRepository reportRepo = new ReportRepository();
            var report = reportRepo.GetReport(id);
            if (report.Single)
                report.SingleChoices = reportRepo.GetSingleList<TRepository>(report);
            foreach (var filter in report.Filters)
                if (filter.IsListFilter || filter.IsValueFilter)
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

            if (report.Chart)
            {
                return GenerateChartReport(report, result);
            }
            else if (report.UiHint.NotNull())
            {
                ViewBag.Title = report.Name;
                ViewBag.Description = report.Description;
                ViewBag.Filters = report.Filters.BuildFilterHeading(true);
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

                    if (ienumerableResult.GetType().ImplementsIEnumerable())
                    {
                        var reportGenericType = ienumerableResult.GetType().GetGenericArguments().Last();
                        foreach (var prop in reportGenericType.GetProperties())
                        {
                            var displayAttribute = prop.GetCustomAttributes(typeof(DisplayAttribute), true).SingleOrDefault() as DisplayAttribute;
                            if (displayAttribute != null && displayAttribute.GetAutoGenerateField().HasValue && !displayAttribute.GetAutoGenerateField().Value)
                            {
                                var field = doddleReport.DataFields[prop.Name];
                                if (field.NotNull())
                                    field.Hidden = true;
                            }
                        }
                    }


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

        private ActionResult GenerateChartReport(Joe.Business.Report.Report report, object result)
        {
            var renderFunction = "renderReport" + report.Name.Replace(" ", String.Empty);
            ViewBag.RenderFunction = renderFunction;
            var chartTypeStr = this.Request.QueryString["ChartType"];
            ViewBag.Filters = report.Filters.BuildFilterHeading(true);
            if (typeof(IEnumerable).IsAssignableFrom(result.GetType()))
            {

                if (typeof(IChartPoint).IsAssignableFrom(report.ReportView))
                {
                    var iChartPoints = ((IEnumerable)result).Cast<IChartPoint>();

                    var seriesData = iChartPoints.GroupBy(g => g.Series).Select(group => new Series
                    {
                        Name = group.Key.ToString(),
                        Data = new Data(group.To2DimensionalArray()),
                    }).ToArray();

                    DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts("chart")
                                                                .InitChart(new Chart()
                                                                {
                                                                    Type = this.GetChartType(report),
                                                                    ClassName = "chart",
                                                                })
                                                               .SetSeries(seriesData)
                                                               .SetTitle(new Title() { Text = report.Name });
                    chart.InFunction(renderFunction);
                    //SetDefaultXAxis(iChartPoints, chart);
                    return View("Chart", chart);
                }
                else if (typeof(IChartReport).IsAssignableFrom(report.ReportView))
                {
                    var resultEnumerable = ((IEnumerable)result).Cast<IChartReport>();
                    var firstResult = resultEnumerable.FirstOrDefault();

                    if (firstResult.NotNull())
                    {
                        var seriesData = resultEnumerable.Select(point =>
                                         new Series()
                                         {
                                             Name = point.Series.ToString(),
                                             Data = new Data(
                                                 ((IEnumerable)point.Data).Cast<IPoint>().GroupBy(reportGrouping => reportGrouping.X)
                                                 .Select(g => new ChartPoint() { Y = g.Sum(p => p.Y.ToDouble()), X = g.Key }).To2DimensionalArray())
                                         }).ToArray();

                        DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts("chart")
                                                                   .InitChart(new Chart()
                                                                   {
                                                                       Type = this.GetChartType(report),
                                                                       ClassName = "chart",
                                                                   })
                                                                  .SetSeries(seriesData)
                                                                  .SetTitle(new Title() { Text = report.Name });

                        if (firstResult.XAxis.NotNull())
                            chart.SetXAxis(new XAxis
                            {
                                Categories = firstResult.XAxis.ToArray()
                            });
                        else
                        {
                            var iChartPoints = resultEnumerable.SelectMany(r => r.Data.Cast<IChartPoint>());
                            SetDefaultXAxis(iChartPoints, chart);
                        }
                        if (firstResult.YAxis.NotNull())
                            chart.SetYAxis(new YAxis
                            {
                                Categories = firstResult.YAxis.ToArray()
                            });
                        chart.InFunction(renderFunction);

                        return View("Chart", chart);
                    }
                }
                return View("Chart");
            }
            else
            {
                if (result != null)
                {
                    var iChartReport = (IChartReport)result;
                    DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts("chart")
                        .InitChart(new Chart()
                        {
                            Type = this.GetChartType(report),
                            ClassName = "chart",
                        });
                    var series = new Series()
                    {
                        Name = iChartReport.Series.ToString(),
                        Data = new Data(
                            ((IEnumerable)iChartReport.Data).Cast<IPoint>().GroupBy(reportGrouping => reportGrouping.X)
                            .Select(g => new ChartPoint() { Y = g.Sum(p => p.Y.ToDouble()), X = g.Key }).To2DimensionalArray())
                    };
                    chart.SetSeries(series);
                    chart.SetTitle(new Title() { Text = report.Name });

                    if (iChartReport.XAxis.NotNull())
                        chart.SetXAxis(new XAxis
                        {
                            Categories = iChartReport.XAxis.ToArray()
                        });
                    else
                    {
                        SetDefaultXAxis(iChartReport.Data.Cast<IChartPoint>(), chart);
                    }
                    if (iChartReport.YAxis.NotNull())
                        chart.SetYAxis(new YAxis
                        {
                            Categories = iChartReport.YAxis.ToArray()
                        });
                    chart.InFunction(renderFunction);

                    return View("Chart", chart);
                }

                return View("Chart");
            }
        }

        private void SetDefaultXAxis(IEnumerable<IPoint> iChartPoints, DotNet.Highcharts.Highcharts chart)
        {
            var xAxisList = iChartPoints.Where(p => p.X != null).Select(p => p.X.ToString()).Distinct().ToList();
            if (xAxisList.Count > 0)
            {
                xAxisList.Sort();
                chart.SetXAxis(new XAxis()
                {
                    Categories = xAxisList.ToArray()
                });
            }
        }

        public virtual IEnumerable<IReport> FilterReports(IEnumerable<IReport> reports)
        {
            return reports;
        }

        private DotNet.Highcharts.Enums.ChartTypes GetChartType(Joe.Business.Report.IReport report)
        {
            var chartTypeStr = this.Request.QueryString["ChartType"];
            if (chartTypeStr != null)
                switch (chartTypeStr.ToLower())
                {
                    case "bar":
                        return DotNet.Highcharts.Enums.ChartTypes.Bar;
                    case "pie":
                        return DotNet.Highcharts.Enums.ChartTypes.Pie;
                    case "scatter":
                        return DotNet.Highcharts.Enums.ChartTypes.Scatter;
                    case "line":
                        return DotNet.Highcharts.Enums.ChartTypes.Line;
                    case "bubble":
                        return DotNet.Highcharts.Enums.ChartTypes.Bubble;
                    case "spline":
                        return DotNet.Highcharts.Enums.ChartTypes.Spline;
                    case "area":
                        return DotNet.Highcharts.Enums.ChartTypes.Area;
                    case "areaspline":
                        return DotNet.Highcharts.Enums.ChartTypes.Areaspline;
                    case "arearange":
                        return DotNet.Highcharts.Enums.ChartTypes.Arearange;
                    case "areasplinerange":
                        return DotNet.Highcharts.Enums.ChartTypes.Areasplinerange;
                    case "columnrange":
                        return DotNet.Highcharts.Enums.ChartTypes.Columnrange;
                    case "funnel":
                        return DotNet.Highcharts.Enums.ChartTypes.Funnel;
                    case "guage":
                        return DotNet.Highcharts.Enums.ChartTypes.Gauge;
                    default:
                        return DotNet.Highcharts.Enums.ChartTypes.Column;
                }

            return (DotNet.Highcharts.Enums.ChartTypes)Enum.Parse(typeof(DotNet.Highcharts.Enums.ChartTypes), report.ChartType.ToString());
        }

        private class ChartPoint : IPoint
        {
            public Object X { get; set; }
            public Object Y { get; set; }
        }
    }
}
