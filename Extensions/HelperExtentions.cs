using System;
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

namespace Joe.Web.Mvc.Utility.Extensions
{
    public static class HelperExtentions
    {
        public static Boolean NotNull(this Object obj)
        {
            return obj != null;
        }

        public static Nullable<T> ToNullable<T>(this string s) where T : struct
        {
            Nullable<T> result = new Nullable<T>();
            try
            {
                if (!string.IsNullOrEmpty(s) && s.Trim().Length > 0)
                {
                    TypeConverter conv = TypeDescriptor.GetConverter(typeof(T));
                    result = (T)conv.ConvertFrom(s);
                }
            }
            catch
            { }
            return result;
        }

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
                var textString = text.Compile().Invoke(item).ToString();
                selectList.Add(new SelectListItem() { Value = valueString, Text = textString, Selected = valueString == (selected.NotNull() ? selected.ToString() : String.Empty) });
            }

            return selectList;
        }

        public static void LogError(this Exception ex)
        {
            if (BaseController.ErrorLogger.NotNull())
                BaseController.ErrorLogger.LogError(ex);
        }

        public static void CleanDataAttributes(this IDictionary<String, Object> dictionary)
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

        public static String BuildFilterHeading(this IEnumerable<IReportFilter> filters)
        {
            if (filters.NotNull())
            {
                String filterHeader = null;
                foreach (var filter in filters)
                {
                    var display = filter.DisplayAttribute != null ? filter.DisplayAttribute.Name : filter.PropertyName;
                    if (filterHeader == null)
                        filterHeader = display + ": " + filter.Value;
                    else
                        filterHeader += Environment.NewLine + display + ": " + filter.Value;
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
    }
}