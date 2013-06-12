using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using Joe.Web.Mvc.Utility.Session;
using System.Text;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Joe.Business.Report;
using Joe.Map;



namespace Joe.Web.Mvc.Utility.Extensions
{
    public static class HtmlExtentions
    {
        #region GroupBy

        #region EditorForGroupBy

        public static MvcHtmlString EditorForGroupBy<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, String groupByString, String propertyName = null, String containerTag = null, String headerTag = null, String groupByTag = null, Object groupContainerAttributes = null, Object groupByTagAttributes = null, Object headerTagAttributes = null)
            where TValue : IEnumerable
        {
            return GroupBy(html, expression, groupByString, propertyName, containerTag, headerTag, groupByTag,
                                             new RouteValueDictionary(groupContainerAttributes),
                                             new RouteValueDictionary(groupByTagAttributes),
                                             new RouteValueDictionary(headerTagAttributes), true);
        }

        #endregion

        #region DisplayForGroupBy

        public static MvcHtmlString DisplayForGroupBy<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, String groupByString = null, String containerTag = null, String headerTag = null, String groupByTag = null, Object groupContainerAttributes = null, Object groupByTagAttributes = null, Object headerTagAttributes = null)
            where TValue : IEnumerable
        {
            return GroupBy(html, expression, groupByString, null, containerTag, headerTag, groupByTag,
                                             new RouteValueDictionary(groupContainerAttributes),
                                             new RouteValueDictionary(groupByTagAttributes), new RouteValueDictionary(headerTagAttributes), false);
        }

        #endregion

        private static MvcHtmlString GroupBy<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, String groupByString, String propertyName, String containerTag, String headerTag, String groupByTag, IDictionary<String, Object> groupContainerAttributes, IDictionary<String, Object> groupByTagAttributes, IDictionary<String, Object> headerTagAttributes, Boolean editorFor)
          where TValue : IEnumerable
        {
            var enumerable = expression.Compile().Invoke(html.ViewData.Model);
            var genericType = typeof(TValue).GetGenericArguments().SingleOrDefault();
            if (genericType == null) throw new ArgumentNullException("genericType", "TValue must be a Generic IEnumerable");
            var groupedEnumerable =
                enumerable.Cast<Object>().GroupBy(enumType => Joe.Reflection.ReflectionHelper.GetEvalProperty(enumType, groupByString));

            var container = new TagBuilder(containerTag ?? "ul");
            container.MergeAttributes(groupContainerAttributes);
            var i = 0;
            foreach (var group in groupedEnumerable)
            {
                var key = new TagBuilder(headerTag ?? "li");
                key.MergeAttributes(headerTagAttributes);
                var groupByTagBuilder = new TagBuilder(groupByTag ?? "ul");
                groupByTagBuilder.MergeAttributes(groupByTagAttributes);
                key.InnerHtml = headerTag == "fieldset" ? new TagBuilder("legend") { InnerHtml = group.Key.ToString() }.ToString() : group.Key.ToString();
                foreach (var item in group)
                {
                    var name = propertyName ?? expression.ToString().Remove(0, expression.ToString().IndexOf('.') + 1);
                    name = String.Format("{0}[{1}]", name, i);
                    groupByTagBuilder.InnerHtml += editorFor ? html.EditorFor(model => item, genericType.Name, name) : html.DisplayFor(model => item, genericType.Name, name);
                    i++;
                }
                key.InnerHtml += groupByTagBuilder.ToString();

                container.InnerHtml += key.ToString();
            }

            return MvcHtmlString.Create(container.ToString());
        }

        #endregion

        #region Paging/Sorting
        public static MvcHtmlString Pager<TModel>(this HtmlHelper<TModel> html, Object containerAttributes = null, Object recordsAttributes = null, int? count = null, String updateID = null, String url = null)
        {
            return Pager(html, containerAttributes.NotNull() ? new RouteValueDictionary(containerAttributes) : null,
                recordsAttributes.NotNull() ? new RouteValueDictionary(recordsAttributes) : null, count, updateID, url);
        }

        public static MvcHtmlString Pager<TModel>(this HtmlHelper<TModel> html, IDictionary<String, Object> containerAttributes, IDictionary<String, Object> recordsAttributes, int? count, String updateID, String url)
        {
            var ViewBag = html.ViewBag;
            count = count ?? ViewBag.Count ?? 0;
            int page = (ViewBag.Skip ?? 0) / (ViewBag.Take ?? Configuration.ConfigurationHelper.PageSize);
            int start = (ViewBag.Skip ?? 0) + 1;
            int end = (ViewBag.Skip ?? 0) + (ViewBag.Take ?? Configuration.ConfigurationHelper.PageSize);
            int previousStart = (ViewBag.Skip ?? 0) - (ViewBag.Take ?? Configuration.ConfigurationHelper.PageSize);
            var ordered = !String.IsNullOrEmpty(ViewBag.OrderBy);
            var hasWhere = !String.IsNullOrEmpty(ViewBag.Where);
            var orderby = ViewBag.OrderBy;
            var descending = ViewBag.descending ?? false;
            var ajax = !String.IsNullOrEmpty(updateID);
            url = url ?? html.ViewContext.RequestContext.HttpContext.Request.Path;
            var ajaxAttributes = new Dictionary<String, String>();
            ajaxAttributes.Add("data-ajax", "true");
            ajaxAttributes.Add("data-ajax-update", updateID);
            if (end > count)
                end = count.Value;

            var container = new TagBuilder("div");
            container.MergeAttributes(containerAttributes);
            var ul = new TagBuilder("ul");

            var prev = new TagBuilder("li");
            var prevLink = new TagBuilder("a");
            prevLink.InnerHtml = "&larr;Prev";
            if (ajax)
                prevLink.MergeAttributes(ajaxAttributes);

            String prevQueryString = String.Format("?take={0}&skip={1}", ViewBag.Take, previousStart) + html.GetCurrentOrderByQueryString() + html.GetCurrentWhereQueryString();
            prevLink.Attributes.Add("href", url + prevQueryString);
            if (page == 0)
                prev.Attributes.Add("class", "disabled");

            prev.InnerHtml = prevLink.ToString();

            var next = new TagBuilder("li");
            var nextLink = new TagBuilder("a");
            nextLink.InnerHtml = "Next&rarr;";
            if (ajax)
                nextLink.MergeAttributes(ajaxAttributes);

            String nextQueryString = String.Format("?take={0}&skip={1}", ViewBag.Take, end) + html.GetCurrentOrderByQueryString() + html.GetCurrentWhereQueryString();
            nextLink.Attributes.Add("href", url + nextQueryString);

            if (count <= end)
                next.Attributes.Add("class", "disabled");

            next.InnerHtml = nextLink.ToString();

            var records = new TagBuilder("span");
            records.MergeAttributes(recordsAttributes);
            records.InnerHtml = String.Format("Viewing {0}-{1} of {2}", count == 0 ? count : start, end, count);

            ul.InnerHtml = prev.ToString() + next;
            container.InnerHtml = ul.ToString();

            return new MvcHtmlString(container.ToString() + records.ToString());

        }

        public static MvcHtmlString SortFor<TModel, TProperty>(this HtmlHelper<IEnumerable<TModel>> html, Expression<Func<TModel, TProperty>> expression, String updateID = null, String url = null)
        {
            var name = ((MemberExpression)expression.Body).Member.Name;
            var sorted = html.ViewBag.OrderBy == name;
            var take = (html.ViewBag.Take ?? Configuration.ConfigurationHelper.PageSize);
            var skip = (html.ViewBag.Skip ?? 0);
            var descending = html.ViewBag.Descending ?? false;
            var ajax = !String.IsNullOrEmpty(updateID);
            url = url ?? String.Empty;
            var ajaxAttributes = new Dictionary<String, String>();
            ajaxAttributes.Add("data-ajax", "true");
            ajaxAttributes.Add("data-ajax-update", updateID);

            var headerLink = new TagBuilder("a");

            headerLink.InnerHtml = html.DisplayNameFor(expression).ToString();
            if (ajax)
                headerLink.MergeAttributes(ajaxAttributes);
            if (sorted)
            {
                var caret = new TagBuilder("span");
                caret.AddCssClass(descending ? "sortdesc" : "sortasc");
                headerLink.InnerHtml += caret.ToString();
            }
            var where = html.ViewBag.Where;
            var queryString = String.Format("?orderby={0}&descending={1}", name, !descending) + html.GetCurrentPageQueryString() + html.GetCurrentWhereQueryString();

            headerLink.MergeAttribute("href", url + queryString);

            return new MvcHtmlString(headerLink.ToString());
        }

        #endregion

        private static String GetCurrentOrderByQueryString(this HtmlHelper html, Boolean and = true)
        {
            var sorted = !String.IsNullOrEmpty(html.ViewBag.OrderBy);

            if (sorted)
                return String.Format("{0}orderby={1}&descending={2}", and ? "&" : String.Empty, html.ViewBag.OrderBy, html.ViewBag.Descending ?? false);

            return null;

        }

        private static String GetCurrentWhereQueryString(this HtmlHelper html, Boolean and = true)
        {
            var filtered = !String.IsNullOrEmpty(html.ViewBag.Where);

            if (filtered)
                return String.Format("{0}where={1}", and ? "&" : String.Empty, html.ViewBag.Where);

            return null;
        }

        private static String GetCurrentPageQueryString(this HtmlHelper html, Boolean and = true)
        {
            var paged = html.ViewBag.Take != null;

            if (paged)
                return String.Format("{0}take={1}&skip={2}", and ? "&" : String.Empty, html.ViewBag.Take, html.ViewBag.Skip);

            return null;
        }

        public static MvcHtmlString WhereLinkFor<TModel>(this HtmlHelper<IEnumerable<TModel>> html, String url, String whereClause, String text, String ajaxLoad = null, Object htmlAttributes = null)
        {
            var link = new TagBuilder("a");
            url += String.Format("?where={0}", whereClause) + html.GetCurrentOrderByQueryString();
            if (htmlAttributes.NotNull())
            {
                var linkAttributes = new RouteValueDictionary(htmlAttributes);
                link.MergeAttributes(linkAttributes);
            }
            if (ajaxLoad.NotNull())
            {
                link.MergeAttribute("data-ajax", true.ToString().ToLower());
                link.MergeAttribute("data-ajax-update", ajaxLoad);
            }
            if (html.ViewBag.Where == whereClause)
                link.AddCssClass("active");
            link.MergeAttribute("href", url);
            link.InnerHtml = text;
            return new MvcHtmlString(link.ToString());
        }

        //public static MvcHtmlString WhereLinkListFor(this HtmlHelper html, IEnumerable<T>  String url, String whereClause, String text, String ajaxLoad = null, Object htmlAttributes = null)
        //{
        //    var link = new TagBuilder("a");
        //    url += String.Format("?where={0}", whereClause) + html.GetCurrentOrderByQueryString();
        //    if (htmlAttributes.NotNull())
        //    {
        //        var linkAttributes = new RouteValueDictionary(htmlAttributes);
        //        link.MergeAttributes(linkAttributes);
        //    }
        //    if (ajaxLoad.NotNull())
        //    {
        //        link.MergeAttribute("data-ajax", true.ToString().ToLower());
        //        link.MergeAttribute("data-ajax-update", ajaxLoad);
        //    }
        //    if (html.ViewBag.Where == whereClause)
        //        link.AddCssClass("active");
        //    link.MergeAttribute("href", url);
        //    link.InnerHtml = text;
        //    return new MvcHtmlString(link.ToString());
        //}

        public static MvcHtmlString LinkFor<TModel>(this HtmlHelper<IEnumerable<TModel>> html, String url, String text, Object htmlAttributes = null)
        {
            var link = new TagBuilder("a");
            url += "?" + html.GetCurrentOrderByQueryString(false);
            if (htmlAttributes.NotNull())
            {
                var linkAttributes = new RouteValueDictionary(htmlAttributes);
                link.MergeAttributes(linkAttributes);
            }
            link.MergeAttribute("href", url);
            link.InnerHtml = text;
            return new MvcHtmlString(link.ToString());
        }

        public static Boolean ViewExists(this HtmlHelper html, String viewName)
        {
            ViewEngineResult result = ViewEngines.Engines.FindView(html.ViewContext.Controller.ControllerContext, viewName, null);
            return (result.View != null);
        }

        public static MvcHtmlString GroupedSelectList<TModel, TValue, TList, TOptValue, TDisplayText, TGroupBy>(this HtmlHelper<TModel> html,
            Expression<Func<TModel, IEnumerable<TList>>> listExpression,
            Expression<Func<TModel, TValue>> keyProperty,
            Expression<Func<TList, TOptValue>> optionsValue,
            Expression<Func<TList, TDisplayText>> displayTextExpression,
            Expression<Func<TList, TGroupBy>> groupByProperty,
            Object htmlAttributes = null,
            Boolean includeEmpty = false)
        {
            var optionList = listExpression.Compile().Invoke(html.ViewData.Model);
            var selectTag = new TagBuilder("select");
            selectTag.Attributes.Add("name", html.NameFor(keyProperty).ToString());
            selectTag.GenerateId(html.IdFor(keyProperty).ToString());

            if (htmlAttributes.NotNull())
            {
                var attributesDict = new RouteValueDictionary(htmlAttributes);
                attributesDict.CleanDataAttributes();
                selectTag.MergeAttributes(attributesDict);
            }

            if (includeEmpty)
                selectTag.InnerHtml += new TagBuilder("option").ToString();

            var selectedValue = keyProperty.Compile().Invoke(html.ViewData.Model);
            foreach (var group in optionList.GroupBy(groupByProperty.Compile()))
            {
                var optGroupTag = new TagBuilder("optgroup");
                optGroupTag.Attributes.Add("label", group.Key.ToString());

                foreach (var item in group)
                {
                    var optionTag = new TagBuilder("option");
                    var value = optionsValue.Compile().Invoke(item);
                    optionTag.Attributes.Add("value", value.ToString());
                    optionTag.InnerHtml = displayTextExpression.Compile().Invoke(item).ToString();

                    if (value.Equals(selectedValue))
                        optionTag.MergeAttribute("selected", "selected");

                    optGroupTag.InnerHtml += optionTag.ToString();
                }
                selectTag.InnerHtml += optGroupTag.ToString();
            }

            return new MvcHtmlString(selectTag.ToString());
        }

        #region Radio Button List

        public static MvcHtmlString RadioButonListFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IDictionary<String, String> radioButtonList)
        {
            return RadioButonListFor(html, expression, radioButtonList, null);
        }

        public static MvcHtmlString RadioButonListFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IDictionary<String, String> radioButtonList, Object htmlAttributes)
        {

            return RadioButonListFor(html, expression, radioButtonList, new RouteValueDictionary(htmlAttributes));
        }

        public static MvcHtmlString RadioButonListFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IDictionary<String, String> radioButtonList, IDictionary<String, Object> htmlAttributes)
        {
            StringBuilder returnList = new StringBuilder();
            foreach (KeyValuePair<String, String> item in radioButtonList)
            {
                TagBuilder container = new TagBuilder("label");
                container.MergeAttributes(htmlAttributes);
                container.InnerHtml += html.RadioButtonFor<TModel, TProperty>(expression, item.Value) + item.Key;
                returnList.Append(container.ToString());
            }

            return new MvcHtmlString(returnList.ToString());
        }

        #endregion

        public static MvcHtmlString EditForReportFilter<TModel>(this HtmlHelper<TModel> html, TModel filter)
           where TModel : ReportFilter
        {
            var placeholder = filter.DisplayAttribute != null ? filter.DisplayAttribute.Name : filter.PropertyName;
            if (!filter.IsListFilter)
                if (typeof(DateTime).IsAssignableFrom(filter.FilterType))
                {
                    return html.TextBoxFor(model => model.Value, null, new { @class = "date", placeholder = placeholder });
                }
                else if (typeof(Boolean).IsAssignableFrom(filter.FilterType))
                {
                    TagBuilder label = new TagBuilder("label");
                    label.AddCssClass("checkbox");
                    label.InnerHtml = html.CheckBox("Value").ToString() + placeholder;
                    return new MvcHtmlString(label.ToString());
                }
                else
                {
                    return html.TextBoxFor(model => model.Value, new { placeholder = placeholder });
                }
            else
                return html.DropDownListFor(model => model.Value, filter.ListValues.Cast<Object>().ToSelectList(
                    item => filter.ValueProperty.NotNull() ? Joe.Reflection.ReflectionHelper.GetEvalProperty(item, filter.ValueProperty) : item.GetIDs().BuildIDList(),
                    item => item.ConcatDisplayProperties(filter.DisplayProperties), null), String.Empty,
                    new { @class = "chosen", placeholder = placeholder, data_placeholder = placeholder });

            //if (typeof(String).IsAssignableFrom(filter.FilterType)
            //   || typeof(int).IsAssignableFrom(filter.FilterType)
            //   || typeof(int?).IsAssignableFrom(filter.FilterType)
            //   || typeof(decimal?).IsAssignableFrom(filter.FilterType)
            //   || typeof(decimal).IsAssignableFrom(filter.FilterType)
            //   || typeof(long?).IsAssignableFrom(filter.FilterType)
            //   || typeof(long).IsAssignableFrom(filter.FilterType))
            //{
            //    return html.TextBoxFor(model => model.Value);
            //}
        }

    }
}