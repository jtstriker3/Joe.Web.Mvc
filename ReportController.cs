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
using DoddleReport.Configuration;
using DoddleReport.ReportSources;
using Joe.Web.Mvc.Extensions.DoddleReport;
using System.Dynamic;

namespace Joe.Web.Mvc
{
    public abstract class ReportController : Controller
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
                report.SingleChoices = reportRepo.GetSingleList(report);
            foreach (var filter in report.Filters)
                if (filter.IsListFilter || filter.IsValueFilter)
                    filter.ListValues = reportRepo.GetFilterValues(filter);

            SetFilterDefaults(report.Filters);
            return this.Request.IsAjaxRequest() ? PartialView(report) : (ActionResult)View(report);
        }

        public ActionResult Run([Bind(Exclude = "ReportFilterAttribute")] Joe.Business.Report.Report report)
        {
            IReportRepository reportRepo = new ReportRepository();
            IReport outReport;
            var result = reportRepo.Run(report, out outReport);
            report = (Joe.Business.Report.Report)outReport;
            var extension = this.Request.RequestContext.RouteData.Values["extension"];
            var reportFromView = reportRepo.GetReport(report.Name);

            if (report.Chart)
            {
                return GenerateChartReport((IChartReport)report, reportRepo, result);
            }
            else if (report.UiHint.NotNull())
            {
                ViewBag.Title = report.Name;
                ViewBag.Description = report.Description;
                ViewBag.Filters = report.Filters.BuildFilterHeading(reportRepo, true);
                var embed = Convert.ToBoolean(Request.QueryString["embed"]);
                if (typeof(IEnumerable).IsAssignableFrom(result.GetType()))
                {
                    var ienumerableResult = (IEnumerable)result;
                    var where = this.HttpContext.Request.QueryString["where"];
                    if (where.NotNull())
                        ienumerableResult = ienumerableResult.Filter(where);

                    return embed ? PartialView(report.UiHint, ienumerableResult) : (ActionResult)View(report.UiHint, ienumerableResult);
                }

                return embed ? PartialView(report.UiHint, result) : (ActionResult)View(report.UiHint, result);
            }
            else
            {
                DoddleReport.Report doddleReport = new DoddleReport.Report();
                WriterElement writerElement = this.GetWriterElement();
                var isNotHtml = false;

                if (extension != null && !extension.ToString().ToLower().Contains("html"))
                    isNotHtml = true;
                else if (writerElement != null && !writerElement.FileExtension.ToLower().Contains("html"))
                    isNotHtml = true;

                var reportCssUrl = UrlHelper.GenerateContentUrl("~/content/report.css", this.HttpContext);
                if (!isNotHtml)
                    doddleReport.TextFields.Title = String.Format("<link href='{0}' rel='stylesheet' />", reportCssUrl) + reportFromView.Name;
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

                    if (this.IsDyanmic(ienumerableResult))
                        doddleReport.Source = ienumerableResult.ToDynamicReportSource();
                    else
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
                    var hasIenumerableProperty = result.GetType().GetProperties().Any(prop => prop.PropertyType.ImplementsIEnumerable());
                    var reportList = result.UnionAllList();
                    if (!hasIenumerableProperty)
                        reportList = new List<Object>() { result };

                    doddleReport.TextFields.Header = result.BuildReportHeading(report.Filters);

                    if (this.IsDyanmic(reportList))
                        doddleReport.Source = reportList.ToDynamicReportSource();
                    else
                        doddleReport.Source = reportList.ToReportSource();

                    if (reportList.Count() > 0)
                    {
                        var reportGenericType = reportList.First().GetType();
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


                    //foreach (var childReport in result.BuildChildReports())
                    //{
                    //    doddleReport.AppendReport(childReport);
                    //}

                }

                doddleReport.TextFields.Header += Environment.NewLine + (isNotHtml ? "Filters" + Environment.NewLine : "<b>Filters</b><br/>") + report.Filters.BuildFilterHeading(reportRepo);
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

                if (writerElement != null)
                    return new ReportResult(doddleReport, DoddleReport.Configuration.Config.Report.Writers.GetWriterByName(writerElement.Format), writerElement.ContentType) { FileName = report.Name + writerElement.FileExtension };
                else
                    return new ReportResult(doddleReport);
            }
        }

        private ActionResult GenerateChartReport(Joe.Business.Report.IChartReport report, IReportRepository repo, object result)
        {
            var renderFunction = "renderReport" + report.Name.Replace(" ", String.Empty);
            ViewBag.RenderFunction = renderFunction;
            var chartTypeStr = this.Request.QueryString["ChartType"];
            ViewBag.Filters = report.Filters.BuildFilterHeading(repo, true);
            if (typeof(IEnumerable).IsAssignableFrom(result.GetType()))
            {

                if (typeof(IChartPoint).IsAssignableFrom(result.GetType().GetGenericArguments().FirstOrDefault()))
                {
                    var iChartPoints = ((IEnumerable)result).Cast<IChartPoint>();
                    iChartPoints = iChartPoints.AddSeries();
                    var seriesData = iChartPoints.GroupBy(g => g.Series).OrderBy(g => g.Key).Select(group => new Series
                    {
                        Name = group.Key.ToString(),
                        Data = new Data(group.To2DimensionalArray()),
                    }).ToList();

                    DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts("chart")
                                                                .InitChart(new Chart()
                                                                {
                                                                    Type = this.GetChartType(report),
                                                                    ClassName = "chart",
                                                                    Height = report.Height
                                                                })
                                                               .SetTitle(new Title() { Text = report.Name });
                    var xAxis = GetXAxis(iChartPoints);
                    chart.SetXAxis(new XAxis()
                    {
                        Type = AxisTypes.Category,
                        Categories = xAxis.ToArray(),
                        Labels = new XAxisLabels()
                        {
                            Rotation = report.XRotation
                        }
                    });

                    if (report.YAxisPlotLines != null)
                    {
                        var plotLines = this.GetYAxisPlotLines(report);
                        chart.SetYAxis(new YAxis
                         {
                             PlotLines = plotLines,
                             Title = new YAxisTitle()
                                    {
                                        Text = report.YAxisText
                                    }
                         });

                        AddPlotSeries(seriesData, plotLines);
                    }
                    else if (report.YAxisText != null)
                    {
                        chart.SetYAxis(new YAxis
                        {
                            Title = new YAxisTitle()
                            {
                                Text = report.YAxisText
                            }
                        });
                    }

                    chart.SetSeries(seriesData.ToArray());
                    this.AddChartLabels(chart, report);
                    chart.InFunction(renderFunction);

                    return this.Request.IsAjaxRequest() ? PartialView("Chart", chart) : (ActionResult)View("Chart", chart);
                }
                else if (typeof(IChartReportResult).IsAssignableFrom(result.GetType().GetGenericArguments().FirstOrDefault()))
                {
                    var resultEnumerable = ((IEnumerable)result).Cast<IChartReportResult>();
                    var firstResult = resultEnumerable.FirstOrDefault();

                    if (firstResult.NotNull())
                    {
                        DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts("chart");

                        IEnumerable<String> xAxis;
                        if (firstResult.XAxis.NotNull())
                            xAxis = firstResult.XAxis;
                        else
                            xAxis = GetXAxis(resultEnumerable.SelectMany(series => series.Data.Cast<IPoint>()).Cast<IPoint>());

                        var seriesData = resultEnumerable.Select(point =>
                                        new Series()
                                        {
                                            Name = point.Series.ToString(),
                                            Data = new Data(
                                                ((IEnumerable)point.Data).Cast<IPoint>().GroupBy(reportGrouping => reportGrouping.X)
                                                .Select(g => new ChartPoint() { Y = g.Sum(p => p.Y.ToDouble()), X = g.Key }).ToDictionary(cp => cp.X.ToString()).AddZeroDataPoints(xAxis))
                                        }).OrderBy(s => s.Name).ToList();

                        if (xAxis.Count() > 0)
                        {
                            //xAxis.Sort();
                            chart.SetXAxis(new XAxis()
                            {
                                Type = AxisTypes.Category,
                                Categories = xAxis.ToArray(),
                                Labels = new XAxisLabels()
                                {
                                    Rotation = report.XRotation
                                }
                            });
                        }


                        if (firstResult.YAxis.NotNull())
                        {
                            var plotLines = this.GetYAxisPlotLines(report);
                            chart.SetYAxis(new YAxis
                               {
                                   Categories = firstResult.YAxis.ToArray(),
                                   PlotLines = plotLines,
                                   Title = new YAxisTitle()
                                   {
                                       Text = report.YAxisText
                                   }
                               });

                            AddPlotSeries(seriesData, plotLines);
                        }
                        else if (report.YAxisPlotLines != null)
                        {
                            var plotLines = this.GetYAxisPlotLines(report);
                            chart.SetYAxis(new YAxis
                            {
                                PlotLines = this.GetYAxisPlotLines(report),
                                Title = new YAxisTitle()
                                {
                                    Text = report.YAxisText
                                }
                            });

                            AddPlotSeries(seriesData, plotLines);
                        }
                        else if (report.YAxisText != null)
                            chart.SetYAxis(new YAxis
                            {
                                Title = new YAxisTitle()
                                {
                                    Text = report.YAxisText
                                }
                            });


                        chart.InitChart(new Chart()
                        {
                            Type = this.GetChartType(report),
                            ClassName = "chart",
                            Height = report.Height
                        })
                       .SetSeries(seriesData.ToArray())
                       .SetTitle(new Title() { Text = report.Name });
                        this.AddChartLabels(chart, report);
                        chart.InFunction(renderFunction);

                        return this.Request.IsAjaxRequest() ? PartialView("Chart", chart) : (ActionResult)View("Chart", chart);
                    }
                }
                return this.Request.IsAjaxRequest() ? PartialView("Chart") : (ActionResult)View("Chart");
            }
            else
            {
                if (result != null)
                {
                    var IChartReportResult = (IChartReportResult)result;
                    DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts("chart")
                        .InitChart(new Chart()
                        {
                            Type = this.GetChartType(report),
                            ClassName = "chart",
                            Height = report.Height
                        });

                    IEnumerable<String> xAxis;

                    if (IChartReportResult.XAxis.NotNull())
                        xAxis = IChartReportResult.XAxis;
                    else
                        xAxis = GetXAxis(IChartReportResult.Data.Cast<IPoint>());

                    if (xAxis.Count() > 0)
                    {
                        //xAxis.Sort();
                        chart.SetXAxis(new XAxis()
                        {
                            Type = AxisTypes.Category,
                            Categories = xAxis.ToArray(),
                            Labels = new XAxisLabels()
                                    {
                                        Rotation = report.XRotation
                                    }
                        });
                    }

                    var series = new Series()
                    {
                        Name = IChartReportResult.Series.ToString(),
                        Data = new Data(
                            ((IEnumerable)IChartReportResult.Data).Cast<IPoint>().GroupBy(reportGrouping => reportGrouping.X)
                            .Select(g => new ChartPoint() { Y = g.ToList().Sum(p => p.Y.ToDouble()), X = g.Key.ToString() }).ToDictionary(cp => cp.X.ToString()).AddZeroDataPoints(xAxis))
                    };

                    var seriesData = new List<Series>() { series };

                    if (IChartReportResult.YAxis.NotNull())
                    {
                        var plotLines = this.GetYAxisPlotLines(report);
                        chart.SetYAxis(new YAxis
                        {
                            Categories = IChartReportResult.YAxis.ToArray(),
                            PlotLines = plotLines,
                            Title = new YAxisTitle()
                            {
                                Text = report.YAxisText
                            }
                        });

                        AddPlotSeries(seriesData, plotLines);
                    }
                    else if (report.YAxisPlotLines != null)
                    {
                        var plotLines = this.GetYAxisPlotLines(report);
                        chart.SetYAxis(new YAxis
                         {
                             PlotLines = plotLines,
                             Title = new YAxisTitle()
                                    {
                                        Text = report.YAxisText
                                    }
                         });

                        AddPlotSeries(seriesData, plotLines);
                    }
                    else if (report.YAxisText != null)
                        chart.SetYAxis(new YAxis
                        {
                            Title = new YAxisTitle()
                            {
                                Text = report.YAxisText
                            }
                        });

                    chart.SetSeries(seriesData.ToArray());
                    chart.SetTitle(new Title() { Text = report.Name });

                    this.AddChartLabels(chart, report);
                    chart.InFunction(renderFunction);

                    return this.Request.IsAjaxRequest() ? PartialView("Chart", chart) : (ActionResult)View("Chart", chart);
                }

                return this.Request.IsAjaxRequest() ? PartialView("Chart") : (ActionResult)View("Chart");
            }
        }

        private static void AddPlotSeries(List<Series> seriesData, YAxisPlotLines[] plotLines)
        {
            var plotSeries = new Series()
            {
                Name = "Plot",
                Data = new Data(plotLines.Select(pl => new Point() { Y = pl.Value }).ToArray()),
                Type = DotNet.Highcharts.Enums.ChartTypes.Scatter,
                LegendIndex = 999,
                Color = plotLines.FirstOrDefault().Color,
                ZIndex = -1
            };

            seriesData.Add(plotSeries);
        }

        private IEnumerable<String> GetXAxis(IEnumerable<IPoint> iChartPoints)
        {
            var xAxis = iChartPoints.Where(p => p.X != null).Select(p => p.X.ToString()).Distinct().ToList();
            //xAxis.Sort();
            return xAxis;
        }

        public virtual IEnumerable<IReport> FilterReports(IEnumerable<IReport> reports)
        {
            return reports;
        }

        private DotNet.Highcharts.Enums.ChartTypes GetChartType(Joe.Business.Report.IChartReport report)
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

        private IList<T> AddZeroDataPoints<T>(IDictionary<Object, T> chartPoints, IEnumerable<String> categories)
            where T : IPoint, new()
        {
            var chartPointList = new List<T>();
            foreach (var category in categories)
            {
                if (chartPoints.ContainsKey(category))
                    chartPoints.Add(category, chartPoints[category]);
                else
                    chartPoints.Add(category, new T() { X = category, Y = 0 });
            }

            return chartPointList;
        }

        private void AddChartLabels(DotNet.Highcharts.Highcharts chart, IChartReport report)
        {
            if (report.ShowLabels)
                chart.SetPlotOptions(new PlotOptions()
                {
                    Bar = new PlotOptionsBar()
                    {
                        DataLabels = new PlotOptionsBarDataLabels()
                        {
                            Align = this.GetAlignment(report.LabelAlign),
                            Color = System.Drawing.ColorTranslator.FromHtml(report.LabelColor),
                            X = report.LabelX,
                            Y = report.LabelY,
                            Rotation = report.LabelAngle,
                            Enabled = true,
                            Shadow = report.LabelShadow,
                            Style = report.LabelStyle
                        }
                    },
                    Column = new PlotOptionsColumn()
                    {
                        DataLabels = new PlotOptionsColumnDataLabels()
                        {
                            Align = this.GetAlignment(report.LabelAlign),
                            Color = System.Drawing.ColorTranslator.FromHtml(report.LabelColor),
                            X = report.LabelX,
                            Y = report.LabelY,
                            Rotation = report.LabelAngle,
                            Enabled = true,
                            Shadow = report.LabelShadow,
                            Style = report.LabelStyle
                        }
                    }
                });
        }

        private DotNet.Highcharts.Enums.HorizontalAligns? GetAlignment(String labelAlign)
        {
            if (labelAlign != null)
                switch (labelAlign.ToLower())
                {
                    case "center":
                        return HorizontalAligns.Center;
                    case "right":
                        return HorizontalAligns.Right;
                    case "left":
                        return HorizontalAligns.Left;
                    default: return null;
                }

            return null;
        }

        private YAxisPlotLines[] GetYAxisPlotLines(IChartReport report)
        {
            var lines = new List<YAxisPlotLines>();
            if (report.YAxisPlotLines != null)
            {
                foreach (var plotLine in report.YAxisPlotLines)
                {
                    var newLine = new YAxisPlotLines();
                    newLine.Value = plotLine.Value;
                    newLine.Width = plotLine.Width;
                    newLine.Color = System.Drawing.ColorTranslator.FromHtml(plotLine.Color);
                    newLine.DashStyle = (DashStyles)((int)plotLine.DashStyle);
                    newLine.ZIndex = 999;
                    newLine.Label = new YAxisPlotLinesLabel()
                    {
                        X = plotLine.Label.X,
                        Y = plotLine.Label.Y,
                        Text = plotLine.Label.Text,
                        Rotation = plotLine.Label.Rotation,
                        Align = this.GetAlignment(plotLine.Label.Align),
                        Style = plotLine.Label.Style
                    };
                    lines.Add(newLine);
                }
            }

            return lines.ToArray();
        }

        private WriterElement GetWriterElement()
        {
            var reportType = this.HttpContext.Request.QueryString["reporttype"];

            if (reportType != null)
                return DoddleReport.Configuration.Config.Report.Writers.Cast<WriterElement>().SingleOrDefault(e => e.Format.ToLower() == reportType.ToLower());

            return null;
        }

        private void SetFilterDefaults(IEnumerable<ReportFilter> filters)
        {
            foreach (var filter in filters)
            {
                var defaultValue = this.Request.QueryString[filter.PropertyName];
                filter.Value = defaultValue;
            }
        }

        private bool IsDyanmic(IEnumerable list)
        {
            var listType = list.GetType();
            if (listType.IsGenericType)
                return typeof(ExpandoObject).IsAssignableFrom(listType.GetGenericArguments().FirstOrDefault());

            return false;
        }
    }

    class ChartPoint : IPoint
    {
        public Object X { get; set; }
        public Object Y { get; set; }
    }
}
