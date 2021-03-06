﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using System.Web.Http.Filters;
using System.Web.Helpers;
using System.Net;
using System.Net.Http;
using System.Web.Mvc;
using System.Linq.Expressions;
using Joe.Map;
using Joe.Business.Report;
using DoddleReport;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Joe.Business;
using Joe.Reflection;
using DotNet.Highcharts.Options;
using Joe.MapBack;

namespace Joe.Web.Mvc.Utility.Extensions
{
    public static class HelperExtentions
    {
        private const String _starGuid = "5503d559602048bf8f36245ec2c52692";
        private const String _lessThanGuid = "2819f0618ea542488a4a7e1696a7879f";
        private const String _greaterThanGuid = "4e678dd22d1b453db7e22a87774899a4";
        private const String _percentGuid = "261389ab7ddc43a69d30747307ac6d82";
        private const String _andGuid = "6e9a7e295d414e9c8e566f002074fe84";
        private const String _colonGuid = "186bbe935ab0438bbb01e9dd28dc1de9";
        private const String _backSlashGuid = "4f5e9d3e15f843cab093947ba518a7a5";

        public static Boolean NotNull(this Object obj)
        {
            return obj != null;
        }

        //public static Nullable<T> ToNullable<T>(this string s) where T : struct
        //{
        //    Nullable<T> result = new Nullable<T>();
        //    try
        //    {
        //        if (!string.IsNullOrEmpty(s) && s.Trim().Length > 0)
        //        {
        //            TypeConverter conv = TypeDescriptor.GetConverter(typeof(T));
        //            result = (T)conv.ConvertFrom(s);
        //        }
        //    }
        //    catch
        //    { }
        //    return result;
        //}

        public static Boolean HasCrudProerties(this Object obj)
        {
            var objType = obj.GetType();
            return objType.GetProperty("CanCreate").NotNull() && objType.GetProperty("CanRead").NotNull() && objType.GetProperty("CanUpdate").NotNull() && objType.GetProperty("CanDelete").NotNull();
        }

        public static IEnumerable<String> BuildExceptionsMessageString(this Exception ex)
        {
            while (ex.NotNull())
            {
                yield return ex.Message;
                ex = ex.InnerException;
            }
        }

        public static Object ErrorObject(this Exception ex)
        {
            return new { errors = ex.BuildExceptionsMessageString() };
        }

        public static void SetErrorResponse(this HttpActionExecutedContext actionExecutedContext, Exception ex)
        {
            ex = ex ?? actionExecutedContext.Exception;
            var response = ex.ErrorObject();
            actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.InternalServerError, response);
        }

        public static IEnumerable<SelectListItem> ToSelectList<T, TValue, TText>(this IEnumerable<T> list, Expression<Func<T, TValue>> value, Expression<Func<T, TText>> text, Object selected)
        {
            List<SelectListItem> selectList = new List<SelectListItem>();

            foreach (var item in list)
            {
                var valueString = value.Compile().Invoke(item).ToString();
                var textValue = text.Compile().Invoke(item);
                String textString = null;
                if (textValue.NotNull())
                    textString = textValue.ToString();
                else
                    textString = "NULL";
                selectList.Add(new SelectListItem() { Value = valueString, Text = textString, Selected = valueString == (selected.NotNull() ? selected.ToString() : String.Empty) });
            }

            return selectList;
        }

        public static void LogError(this Exception ex)
        {
            if (ErrorLogger.LogProvider.NotNull())
                ErrorLogger.LogProvider.LogError(ex);
        }

        public static void CleanDataAttributes(this IDictionary<String, Object> dictionary)
        {
            if (dictionary.NotNull())
            {
                var dictionaryCopy = dictionary.ToDictionary(dict => dict.Key, dict => dict.Value);

                foreach (var item in dictionaryCopy)
                {
                    if (item.Key.StartsWith("data"))
                    {
                        var value = item.Value;
                        var key = item.Key.Replace("_", "-");
                        dictionary.Remove(item.Key);
                        dictionary.Add(key, value);
                    }
                }
            }
        }

        public static IEnumerable<DoddleReport.Report> BuildChildReports(this Object result)
        {
            var doddleReports = new List<DoddleReport.Report>();
            foreach (var ienumerableProperty in result.GetType().GetProperties().Where(prop => prop.PropertyType.ImplementsIEnumerable()))
            {
                var display = ienumerableProperty.GetCustomAttribute<DisplayAttribute>();
                var title = display != null ? display.Name : ienumerableProperty.Name;
                var reportSource = ((IEnumerable)ienumerableProperty.GetValue(result)).ToReportSource();
                DoddleReport.Report doddleReport = new DoddleReport.Report();
                doddleReport.Source = reportSource;
                doddleReport.TextFields.Title = title;
                doddleReports.Add(doddleReport);
            }

            return doddleReports;
        }

        public static IEnumerable<Object> UnionAllList(this Object result)
        {
            var doddleReports = new List<Object>();
            foreach (var ienumerableProperty in result.GetType().GetProperties().Where(prop => prop.PropertyType.ImplementsIEnumerable()))
            {
                var reportSource = ((IEnumerable)ienumerableProperty.GetValue(result));
                doddleReports.AddRange(reportSource.Cast<Object>());
            }

            return doddleReports;
        }

        public static String ConcatDisplayProperties(this Object obj, IEnumerable<String> properties)
        {
            String str = null;

            foreach (var propertyString in properties)
            {
                if (propertyString == null)
                    str = Joe.Reflection.ReflectionHelper.GetEvalProperty(obj, propertyString).ToString();
                else
                    str += " " + Joe.Reflection.ReflectionHelper.GetEvalProperty(obj, propertyString);
            }

            return str;
        }

        public static String BuildReportHeading(this Object obj, IEnumerable<ReportFilter> filters)
        {
            String heading = null;
            foreach (PropertyInfo propInfo in obj.GetType().GetProperties())
            {
                if (propInfo.PropertyType.IsSimpleType())
                {
                    if (!filters.NotNull() || filters.Where(filter => filter.PropertyName == propInfo.Name).Count() == 0)
                    {
                        var display = propInfo.GetCustomAttribute<DisplayAttribute>();
                        var title = display != null ? display.Name : propInfo.Name;

                        if (heading == null)
                            heading = title + ": " + propInfo.GetValue(obj);
                        else
                            heading += Environment.NewLine + title + ": " + propInfo.GetValue(obj);
                    }
                }
            }

            return heading;
        }

        public static String BuildReportHeading(this IReport report)
        {
            var heading = new TagBuilder("h2");
            var description = new TagBuilder("small");
            description.InnerHtml = " " + report.Description;
            heading.InnerHtml = report.Name + description.ToString();
            return heading.ToString();
        }

        public static String BuildIDList(this IEnumerable idList)
        {
            String ids = null;
            foreach (var id in idList)
            {
                if (ids == null)
                    ids = id.ToString();
                else
                    ids += "||" + id.ToString();
            }
            return ids;
        }

        public static String BuildFilterHeading(this IEnumerable<IReportFilter> filters, IReportRepository repository, bool html = false)
        {
            if (filters.NotNull())
            {
                String filterHeader = null;
                foreach (var filter in filters)
                {
                    var display = filter.DisplayAttribute != null ? filter.DisplayAttribute.Name : filter.PropertyName;
                    if (filterHeader == null)

                        if (html)
                        {
                            var div = new TagBuilder("div");
                            var strong = new TagBuilder("strong");
                            strong.InnerHtml = display + ": ";
                            div.InnerHtml = strong.ToString() + (filter.ListView != null && filter.Value != null && filter.GetDisplayFromContext ? repository.GetFilterDisplay(filter) : filter.Value);
                            filterHeader += div.ToString();
                        }
                        else
                            filterHeader = display + ": " + (filter.ListView != null && filter.Value != null && filter.GetDisplayFromContext ? repository.GetFilterDisplay(filter) : filter.Value);
                    else
                        if (html)
                        {
                            var div = new TagBuilder("div");
                            var strong = new TagBuilder("strong");
                            strong.InnerHtml = display + ": ";
                            div.InnerHtml = strong.ToString() + (filter.ListView != null && filter.Value != null && filter.GetDisplayFromContext ? repository.GetFilterDisplay(filter) : filter.Value);
                            filterHeader += div.ToString();
                        }
                        else
                            filterHeader += Environment.NewLine + display + ": " + (filter.ListView != null && filter.Value != null && filter.GetDisplayFromContext ? repository.GetFilterDisplay(filter) : filter.Value);
                }

                return filterHeader;
            }
            return null;
        }

        public static void ForEach<T>(this IEnumerable<T> list, Action<T> action)
        {
            foreach (var item in list)
                action(item);
        }

        public static String Encode(this String stringToEncode)
        {
            stringToEncode = stringToEncode.Replace("*", _starGuid);
            stringToEncode = stringToEncode.Replace("<", _lessThanGuid);
            stringToEncode = stringToEncode.Replace(">", _greaterThanGuid);
            stringToEncode = stringToEncode.Replace("%", _percentGuid);
            stringToEncode = stringToEncode.Replace("&", _andGuid);
            stringToEncode = stringToEncode.Replace(":", _colonGuid);
            stringToEncode = stringToEncode.Replace("\\", _backSlashGuid);
            return HttpUtility.UrlEncode(stringToEncode);
        }

        public static IEnumerable<String> Decode(this IEnumerable<Object> listOfObjectsToDecode)
        {
            foreach (var decodeString in listOfObjectsToDecode)
                yield return decodeString.ToString().Decode();
        }

        public static String Decode(this String stringToDecode)
        {
            HttpUtility.UrlDecode(stringToDecode);
            stringToDecode = stringToDecode.Replace(_starGuid, "*");
            stringToDecode = stringToDecode.Replace(_lessThanGuid, "<");
            stringToDecode = stringToDecode.Replace(_greaterThanGuid, ">");
            stringToDecode = stringToDecode.Replace(_percentGuid, "%");
            stringToDecode = stringToDecode.Replace(_andGuid, "&");
            stringToDecode = stringToDecode.Replace(_colonGuid, ":");
            return stringToDecode.Replace(_backSlashGuid, "\\");
        }

        public static IQueryable TryCast(this IEnumerable list, Type type)
        {
            if (type != null && list.GetType().IsGenericType)
                return ((IEnumerable)Expression.Lambda(Expression.Call(typeof(Enumerable), "Cast", new[] { type }, Expression.Constant(list))).Compile().DynamicInvoke()).AsQueryable();

            return list.AsQueryable();
        }

        public static String BuildFilterString(String filter, Object viewModel)
        {
            var filterProps = filter.Split(',');

            return BuildFilterString(filterProps, viewModel);
        }

        public static String BuildFilterString(IEnumerable<String> filter, Object viewModel)
        {
            String filterString = null;
            foreach (var filterProp in filter)
            {
                if (filterString.NotNull())
                    filterString += ":and:" + filterProp + ":=:" + ReflectionHelper.GetEvalProperty(viewModel, filterProp).ToString();
                else
                    filterString = filterProp + ":=:" + ReflectionHelper.GetEvalProperty(viewModel, filterProp).ToString();
            }
            return filterString;
        }

        public static String BuildFilterString(IEnumerable<String> filter, IEnumerable<Object> ids)
        {
            String filterString = null;
            var count = 0;
            foreach (var filterProp in filter)
            {
                if (filterString.NotNull())
                    filterString += ":and:" + filterProp + ":=:" + ids.ElementAt(count);
                else
                    filterString = filterProp + ":=:" + ids.ElementAt(count);
                count++;
            }
            return filterString;
        }

        public static Boolean IsNumericType(this Type type)
        {
            return type == typeof(System.Byte)
                || type == typeof(System.Byte)
                || type == typeof(System.Byte?)
                || type == typeof(System.Int32)
                || type == typeof(System.Int32?)
                || type == typeof(System.UInt32)
                || type == typeof(System.UInt32?)
                || type == typeof(System.Int16)
                || type == typeof(System.Int16?)
                || type == typeof(System.UInt16)
                || type == typeof(System.UInt16?)
                || type == typeof(System.Int64)
                || type == typeof(System.Int64?)
                || type == typeof(System.UInt64)
                || type == typeof(System.UInt64?)
                || type == typeof(System.Double)
                || type == typeof(System.Double?)
                || type == typeof(System.Decimal)
                || type == typeof(System.Decimal?);
        }

        public static JsonResult AjaxAction(this Controller controller, AjaxActionData ajaxAction)
        {
            controller.Response.AddHeader("X-AjaxAction", "true");
            return new JsonResult() { Data = ajaxAction };
        }

        public static JsonResult AjaxAction(this Controller controller, AjaxActionData ajaxAction, JsonRequestBehavior requestBehavior)
        {
            controller.Response.AddHeader("X-AjaxAction", "true");
            return new JsonResult() { Data = ajaxAction, JsonRequestBehavior = requestBehavior };
        }

        public static double ToDouble(this Object obj)
        {
            double value = 0;
            double.TryParse(obj.ToString(), out value);
            return value;
        }

        public static Object[,] To2DimensionalArray(this IEnumerable<IPoint> points)
        {
            var array = new Object[points.Count(), 2];
            var count = 0;
            foreach (var point in points)
            {
                array[count, 0] = point.X;
                array[count, 1] = point.Y;
                count++;
            }

            return array;
        }

        public static ActionResult AutoRedirect(this Controller controller, String action, String controllerName = null, object routeValues = null)
        {
            var urlHelper = new UrlHelper(controller.Request.RequestContext);
            var url = urlHelper.Action(action, controllerName, routeValues);
            if (controller.Request.IsAjaxRequest())
            {
                return controller.AjaxAction(new AjaxActionData("Redirect", url), JsonRequestBehavior.AllowGet);
            }
            else
                return new RedirectResult(url);
        }

        public static ActionResult AutoRedirect(this Controller controller, String url)
        {
            //var urlHelper = new UrlHelper(controller.Request.RequestContext);
            //var contentUrl = urlHelper.Content(url);
            if (controller.Request.IsAjaxRequest())
            {
                return controller.AjaxAction(new AjaxActionData("Redirect", url), JsonRequestBehavior.AllowGet);
            }
            else
                return new RedirectResult(url);
        }

        internal static Point[] AddZeroDataPoints(this IDictionary<String, ChartPoint> chartPoints, IEnumerable<String> categories)
        {
            var chartPointList = new List<ChartPoint>();
            foreach (var category in categories)
            {
                if (chartPoints.ContainsKey(category))
                    chartPointList.Add(chartPoints[category]);
                else
                    chartPointList.Add(new ChartPoint() { X = category, Y = 0 });
            }

            return chartPointList.Select(cp => new Point() { Y = cp.Y.ToDouble() }).ToArray();
        }

        internal static IEnumerable<IChartPoint> AddSeries(this IEnumerable<IChartPoint> chartPoints)
        {
            var serieses = chartPoints.Select(p => p.Series).Distinct();
            var groupPoints = chartPoints.GroupBy(p => p.X);
            var returnList = new List<IChartPoint>();
            foreach (var group in groupPoints)
            {
                foreach (var series in serieses)
                {
                    if (!group.Select(p => p.Series).Contains(series))
                        returnList.Add(new SeriesChartPoint() { Series = series, X = group.Key, Y = null });
                }
                returnList.AddRange(group);
            }

            return returnList;
        }

        internal class SeriesChartPoint : IChartPoint
        {

            public object Series { get; set; }
            public object X { get; set; }
            public object Y { get; set; }
        }
    }
}