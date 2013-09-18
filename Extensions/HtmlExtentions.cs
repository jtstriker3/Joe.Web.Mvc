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
using System.Web.Mvc.Ajax;



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

            if (groupContainerAttributes.NotNull())
                groupContainerAttributes.CleanDataAttributes();
            if (groupByTagAttributes.NotNull())
                groupByTagAttributes.CleanDataAttributes();
            if (headerTagAttributes.NotNull())
                headerTagAttributes.CleanDataAttributes();

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
        public static MvcHtmlString Pager(this HtmlHelper html, Object containerAttributes = null, Object recordsAttributes = null, Object pagerListAttributes = null, int? count = null, String updateID = null, String url = null, String appendQueryString = null)
        {
            return Pager(html, containerAttributes.NotNull() ? new RouteValueDictionary(containerAttributes) : null,
                recordsAttributes.NotNull() ? new RouteValueDictionary(recordsAttributes) : null,
                pagerListAttributes.NotNull() ? new RouteValueDictionary(pagerListAttributes) : null,
                count, updateID, url, appendQueryString);
        }

        public static MvcHtmlString Pager(this HtmlHelper html, IDictionary<String, Object> containerAttributes, IDictionary<String, Object> recordsAttributes, IDictionary<String, Object> pagerListAttributes, int? count, String updateID, String url, String appendQueryString)
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

            if (containerAttributes.NotNull())
                containerAttributes.CleanDataAttributes();
            if (recordsAttributes.NotNull())
                recordsAttributes.CleanDataAttributes();
            if (pagerListAttributes.NotNull())
                pagerListAttributes.CleanDataAttributes();

            var container = new TagBuilder("div");

            container.MergeAttributes(containerAttributes);
            var ul = new TagBuilder("ul");
            ul.MergeAttributes(pagerListAttributes);

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

            String nextQueryString = String.Format("?take={0}&skip={1}", ViewBag.Take, end) + html.GetCurrentOrderByQueryString() + html.GetCurrentWhereQueryString() + (appendQueryString.NotNull() ? "&" + appendQueryString : null);
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
            return SortFor(html, name, updateID, url);
        }

        public static MvcHtmlString SortFor(this HtmlHelper html, String expression, String updateID = null, String url = null, String headerText = null, String additionalQueryStringValues = null)
        {
            var sorted = html.ViewBag.OrderBy == expression;
            var take = (html.ViewBag.Take ?? Configuration.ConfigurationHelper.PageSize);
            var skip = (html.ViewBag.Skip ?? 0);
            var descending = html.ViewBag.Descending ?? false;
            var ajax = !String.IsNullOrEmpty(updateID);
            url = url ?? String.Empty;
            var ajaxAttributes = new Dictionary<String, String>();
            ajaxAttributes.Add("data-ajax", "true");
            ajaxAttributes.Add("data-ajax-update", updateID);
            var headerLink = new TagBuilder("a");

            headerLink.InnerHtml = headerText ?? html.DisplayName(expression).ToString();
            if (ajax)
                headerLink.MergeAttributes(ajaxAttributes);
            if (sorted)
            {
                var caret = new TagBuilder("span");
                caret.AddCssClass(descending ? "sortdesc" : "sortasc");
                headerLink.InnerHtml += caret.ToString();
            }
            var where = html.ViewBag.Where;
            var queryString = String.Format("?orderby={0}&descending={1}", expression, !descending) + html.GetCurrentPageQueryString() + html.GetCurrentWhereQueryString() + (additionalQueryStringValues.NotNull() ? "&" + additionalQueryStringValues : null);

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
                linkAttributes.CleanDataAttributes();
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
                linkAttributes.CleanDataAttributes();
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

        #region GridListView

        public static MvcHtmlString GridListView<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, int columns = 4, Object rowAttributes = null)
        where TValue : IEnumerable
        {
            return GridListView(html, expression, columns, new RouteValueDictionary(rowAttributes));
        }

        public static MvcHtmlString GridListView<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, int columns, IDictionary<String, Object> rowAttributes)
            where TValue : IEnumerable
        {
            var count = 0;
            rowAttributes.CleanDataAttributes();
            ICollection<TagBuilder> rows = new List<TagBuilder>();
            TagBuilder row = null;
            foreach (var item in expression.Compile().Invoke(html.ViewData.Model))
            {
                if (count % columns == 0)
                {
                    row = new TagBuilder("div");
                    row.MergeAttributes(rowAttributes);
                    rows.Add(row);
                }
                row.InnerHtml += html.DisplayFor(model => item).ToString();
                count++;
            }
            String grid = String.Empty;
            rows.ForEach(r => grid += r.ToString());
            return new MvcHtmlString(grid);
        }

        #endregion

        public static String SetMenuActiveClass(this HtmlHelper html, String controller)
        {
            var currentController = html.ViewContext.RouteData.Values["controller"].ToString();

            if (currentController.ToLower() == controller.ToLower())
                return "active";

            return null;
        }

        public static Boolean HasRole(this HtmlHelper html, params String[] roles)
        {
            return Joe.Security.Security.Provider.IsUserInRole(roles);
        }

        public static MvcForm AutoForm(
            this HtmlHelper htmlHelper,
            string actionName = null,
            string controllerName = null,
            Object routeValues = null,
            FormMethod method = FormMethod.Post,
            Object htmlAttributes = null)
        {
            if (!htmlHelper.ViewContext.HttpContext.Request.IsAjaxRequest())
            {
                return htmlHelper.BeginForm(actionName, controllerName, routeValues, method, htmlAttributes);
            }
            else
            {
                String updateTargetID = htmlHelper.ViewContext.HttpContext.Request.QueryString["UpdateTargetId"];
                String filter = htmlHelper.ViewContext.HttpContext.Request.QueryString["filter"];
                var htmlAttributesDictionary = new RouteValueDictionary(htmlAttributes);
                var routeValueDictionary = new RouteValueDictionary(routeValues);
                routeValueDictionary.Add("UpdateTargetId", updateTargetID);
                routeValueDictionary.Add("filter", filter);

                return new AjaxHelper(htmlHelper.ViewContext, htmlHelper.ViewDataContainer, htmlHelper.RouteCollection).BeginForm(
                    actionName,
                    controllerName,
                    routeValueDictionary,
                    new AjaxOptions
                    {
                        UpdateTargetId = updateTargetID
                    },
                    htmlAttributesDictionary);

            }

        }

        public static MvcHtmlString ModalEditorForList<TModel, TValue>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, IEnumerable<TValue>>> propertyExpression,
            String controllerName,
            Object tableAttributes = null,
            IEnumerable<String> columns = null,
            Boolean showDetails = false,
            Action<TagBuilder, TValue> setRowAttributes = null,
           params String[] keyProperties)
        {

            var currentNameSpace = html.ViewContext.Controller.GetType().Namespace.Replace(".", String.Empty);
            var resouce = Joe.Business.Resource.ResourceProvider.ProviderInstance.GetResource(controllerName + "Controller", currentNameSpace);
            var items = propertyExpression.Compile().Invoke(html.ViewData.Model);
            var updateTargetID = html.ViewContext.HttpContext.Request.QueryString["UpdateTargetId"];
            var tableID = updateTargetID ?? Guid.NewGuid().ToString().Replace("-", String.Empty);
            var container = new TagBuilder("div");



            if (!html.ViewContext.HttpContext.Request.IsAjaxRequest())
            {
                container.InnerHtml = items.Table(html, tableAttributes, tableID, controllerName, true, false, showDetails, true, true, setRowAttributes, columns, keyProperties).ToString();


                var modalContainerID = Guid.NewGuid().ToString().Replace("-", String.Empty);
                var createContainer = BootstrapModal(modalContainerID);

                container.InnerHtml += createContainer.ToString();
                //Create Link

                var keyValues = html.ViewData.Model.GetIDs();
                container.InnerHtml += html.ActionLink("Add New " + resouce,
                    "create", controllerName,
                    new
                    {
                        Set = BuildSetQueryString(keyProperties, keyValues.ToArray()),
                        UpdateTargetId = tableID,
                        Filter = BuildFilterColumnsQueryString(keyProperties)
                    },
                    new
                    {
                        //data_ajax = "true",
                        //data_ajax_update = "#" + createContainerID,
                        data_toggle = "modal",
                        data_target = "#" + modalContainerID,
                        data_backdrop = "false"
                    }).ToString();

                return new MvcHtmlString(container.ToString());
            }
            else
                return items.Table(html, tableAttributes, tableID, controllerName, true, false, showDetails, true, true, setRowAttributes, columns, keyProperties);

        }

        #region Table

        public static MvcHtmlString TableFor<TModel, TValue>(this HtmlHelper<TModel> html,
            Expression<Func<TModel, IEnumerable<TValue>>> propertyExpression,
            Object tableAttributes = null,
            String id = null,
            String controllerName = null,
            Boolean isAjax = true, Boolean readOnly = false,
            Boolean showDetails = true,
            Boolean page = true,
            Boolean sortable = true,
            Action<TagBuilder, TValue> setRowAttributes = null,
            IEnumerable<String> columns = null,
            params String[] filterColumns)
        {
            var items = propertyExpression.Compile().Invoke(html.ViewData.Model);
            return items.Table(html, tableAttributes, id ?? html.ViewContext.HttpContext.Request.QueryString["UpdateTargetId"], controllerName, isAjax, readOnly, showDetails, page, sortable, setRowAttributes, columns, filterColumns);
        }

        public static MvcHtmlString Table<TModel, TValue>(this IEnumerable<TValue> items,
            HtmlHelper<TModel> html,
            Object tableAttributes = null,
            String id = null,
            String controllerName = null,
            Boolean isAjax = true,
            Boolean readOnly = false,
            Boolean showDetails = true,
            Boolean page = true,
            Boolean sortable = true,
            Action<TagBuilder, TValue> setRowAttributes = null,
            IEnumerable<String> columns = null,
            params String[] filterColumns)
        {
            var isAdmin = html.ViewContext.HttpContext.User.IsInRole(Configuration.ConfigurationHelper.AdminRole);
            var crudPropertyNames = new List<String>() { "CanRead", "CanUpdate", "CanCreate", "CanDelete" };
            var filterQuerString = html.ViewContext.HttpContext.Request.QueryString["Filter"];
            var updateIDQueryString = html.ViewContext.HttpContext.Request.QueryString["updateTargetID"];
            var area = html.GetAreaPath();
            var resourceProvider = Joe.Business.Resource.ResourceProvider.ProviderInstance;
            var take = html.ViewContext.HttpContext.Request.QueryString["take"];
            int takeInt = take.NotNull() ? Int32.Parse(take) : Configuration.ConfigurationHelper.PageSize;
            UrlHelper urlHelper = new UrlHelper(html.ViewContext.RequestContext);
            id = id ?? updateIDQueryString ?? Guid.NewGuid().ToString().Replace("-", String.Empty);
            columns = columns ?? new List<String>();
            controllerName = controllerName ?? html.ViewContext.RouteData.Values["controller"].ToString();
            var filterColumnsSetByQueryString = false;
            if (filterColumns.Count() == 0)
            {
                if (filterQuerString.NotNull())
                {
                    filterColumns = filterQuerString.Split(',');
                    filterColumnsSetByQueryString = true;
                }
            }

            var properties = typeof(TValue).GetProperties().Where(prop =>
              prop.PropertyType.IsSimpleType()
              && !prop.Name.ToLower().EndsWith("id")
              && (columns.Count() == 0 || columns.Contains(prop.Name) || crudPropertyNames.Contains(prop.Name))
              && prop.Name != "Included");

            List<PropertyInfo> tempOrderingList = new List<PropertyInfo>();
            if (columns.Count() > 0)
            {
                for (int i = 0; i < columns.Count(); i++)
                {
                    var propInfo = properties.SingleOrDefault(prop => prop.Name == columns.ElementAt(i));
                    if (propInfo.NotNull())
                        tempOrderingList.Add(propInfo);
                }
                properties = tempOrderingList;
            }

            var dictTableAttributes = new RouteValueDictionary(tableAttributes);
            dictTableAttributes.CleanDataAttributes();
            var tableContainer = new TagBuilder("div");
            var table = new TagBuilder("table");

            table.MergeAttributes(dictTableAttributes);
            tableContainer.Attributes.Add("id", id);
            //Build Headers
            var headerRow = new TagBuilder("tr");
            PropertyInfo canUpdateInfo = null;
            PropertyInfo canDeleteInfo = null;
            PropertyInfo canReadInfo = null;

            //This is sloppy Come back and clean up later
            String sortQueryString = null;
            if (!filterColumnsSetByQueryString && isAjax)
            {
                sortQueryString = "where=" + HelperExtentions.BuildFilterString(filterColumns, html.ViewData.Model.GetIDs());
                sortQueryString += "&UpdateTargetId=" + id;
                sortQueryString += "&filter=" + BuildFilterColumnsQueryString(filterColumns);
            }
            else if (isAjax)
            {
                sortQueryString += "UpdateTargetId=" + id;
                sortQueryString += "&filter=" + BuildFilterColumnsQueryString(filterColumns);
            }

            foreach (var property in properties)
            {
                switch (property.Name)
                {
                    case "CanRead":
                        canReadInfo = property;
                        break;
                    case "CanUpdate":
                        canUpdateInfo = property;
                        break;
                    case "CanDelete":
                        canDeleteInfo = property;
                        break;
                }
                if (!crudPropertyNames.Contains(property.Name))
                {
                    var header = new TagBuilder("th");
                    var headerText = String.Empty;
                    var displayAttribute = property.GetCustomAttributes(typeof(DisplayAttribute), true).SingleOrDefault() as DisplayAttribute;
                    Boolean hasResource = false;
                    if (displayAttribute.NotNull())
                    {
                        headerText = displayAttribute.GetName();
                        hasResource = false;
                    }
                    else if (resourceProvider.GetResource(property.Name, property.DeclaringType.Name) != property.Name)
                    {
                        headerText = resourceProvider.GetResource(property.Name, property.DeclaringType.Name);
                        hasResource = true;
                    }
                    else if (property.Name.EndsWith("Name") && !property.Name.StartsWith("Name"))
                    {
                        headerText = property.Name.Replace("Name", String.Empty);
                        hasResource = false;
                    }
                    else
                    {
                        headerText = property.Name;
                        hasResource = false;
                    }

                    header.Attributes.Add("data-property", property.Name);
                    header.Attributes.Add("data-placeholder", headerText);
                    header.Attributes.Add("data-property-type", property.PropertyType.Name);

                    if (sortable)
                        header.InnerHtml = html.SortFor(property.Name, (isAjax ? "#" + id : null), urlHelper.Action("index", controllerName), headerText, sortQueryString).ToString() + GenerateResourceLink(hasResource, html, property.Name, property.DeclaringType.Name);
                    else
                        header.InnerHtml = headerText;
                    headerRow.InnerHtml += header.ToString();
                }
            }
            if (!readOnly || showDetails)
            {
                //Action Row
                headerRow.InnerHtml += new TagBuilder("th");
            }
            table.InnerHtml = headerRow.ToString();
            List<String> itemEditModalIds = new List<string>();
            int count = 0;
            if (items != null)
            {
                //Build table

                //If Table is paged then show Paged Records
                count = items.Count(); ;
                if (page)
                {
                    items = items.Take(takeInt);
                }
                foreach (var item in items)
                {
                    var canUpdate = canUpdateInfo.NotNull() ? (Boolean)canUpdateInfo.GetValue(item, null) : true;
                    var canDelete = canDeleteInfo.NotNull() ? (Boolean)canUpdateInfo.GetValue(item, null) : true;
                    var canRead = canReadInfo.NotNull() ? (Boolean)canUpdateInfo.GetValue(item, null) : true;
                    var editModalId = Guid.NewGuid().ToString().Replace("-", String.Empty);
                    var row = new TagBuilder("tr");
                    if (setRowAttributes.NotNull())
                        setRowAttributes(row, item);

                    foreach (var property in properties)
                    {
                        if (!crudPropertyNames.Contains(property.Name))
                        {
                            var cell = new TagBuilder("td");
                            var value = property.GetValue(item, null);
                            if (value != null)
                                if (value is DateTime)
                                    cell.InnerHtml = ((DateTime)value).ToShortDateString();
                                else
                                    cell.InnerHtml = value.ToString();
                            row.InnerHtml += cell.ToString();
                        }
                    }
                    if (!readOnly)
                    {
                        //Adding Delete and Edit Links
                        var quickEditHref = UrlHelper.GenerateContentUrl("~/" + area + controllerName + "/edit/" + item.GetIDs().ToRoute().Encode() + (isAjax ? "?UpdateTargetId=" + id : String.Empty) + (filterColumns.Count() > 0 ? "&filter=" + BuildFilterColumnsQueryString(filterColumns) : String.Empty), html.ViewContext.HttpContext);
                        var editHref = UrlHelper.GenerateContentUrl("~/" + area + controllerName + "/edit/" + item.GetIDs().ToRoute().Encode(), html.ViewContext.HttpContext);
                        var editLink = new TagBuilder("a");
                        editLink.Attributes.Add("href", quickEditHref);
                        var fullEdit = new TagBuilder("a");
                        fullEdit.Attributes.Add("href", editHref);
                        if (isAjax)
                        {
                            //editLink.Attributes.Add("data-ajax", "true");
                            //editLink.Attributes.Add("data-ajax-update", "#" + id);
                            editLink.Attributes.Add("data-toggle", "modal");
                            editLink.Attributes.Add("data-target", "#" + editModalId);
                            editLink.Attributes.Add("data-backdrop", "false");

                            itemEditModalIds.Add(editModalId);
                        }
                        editLink.InnerHtml = "Quick Edit".GetGlobalResource();
                        fullEdit.InnerHtml = "Edit".GetGlobalResource();
                        var deleteLink = new TagBuilder("a");
                        deleteLink.Attributes.Add("href", UrlHelper.GenerateContentUrl("~/" + area + controllerName + "/delete/" + item.GetIDs().ToRoute().Encode() + (isAjax ? "?UpdateTargetId=" + id : String.Empty) + (filterColumns.Count() > 0 ? "&filter=" + BuildFilterColumnsQueryString(filterColumns) : String.Empty), html.ViewContext.HttpContext));
                        deleteLink.InnerHtml = "Delete".GetGlobalResource();
                        if (isAjax)
                        {
                            deleteLink.Attributes.Add("data-ajax", "true");
                            deleteLink.Attributes.Add("data-ajax-update", "#" + id);
                        }

                        var printView = new TagBuilder("a");
                        var printHref = UrlHelper.GenerateContentUrl("~/" + area + controllerName + "/details/" + item.GetIDs().ToRoute().Encode() + (filterColumns.Count() > 0 ? "&filter=" + BuildFilterColumnsQueryString(filterColumns) : String.Empty), html.ViewContext.HttpContext);
                        printView.InnerHtml += "Details".GetGlobalResource();
                        printView.Attributes.Add("href", printHref);

                        var pullLeftSpan = new TagBuilder("span");
                        pullLeftSpan.AddCssClass("pull-right");
                        pullLeftSpan.InnerHtml = (isAjax ? (canUpdate ? editLink.ToString() : editLink.InnerHtml) + " | " : String.Empty) + (canUpdate ? fullEdit.ToString() : fullEdit.InnerHtml) + " | " + (canDelete ? deleteLink.ToString() : deleteLink.InnerHtml) + (showDetails ? " | " + (canRead ? printView.ToString() : printView.InnerHtml) : String.Empty);
                        row.InnerHtml += new TagBuilder("td") { InnerHtml = pullLeftSpan.ToString() };
                    }
                    else if (showDetails)
                    {
                        var printView = new TagBuilder("a");
                        var printHref = UrlHelper.GenerateContentUrl("~/" + area + controllerName + "/details/" + item.GetIDs().ToRoute().Encode() + (filterColumns.Count() > 0 ? "&filter=" + BuildFilterColumnsQueryString(filterColumns) : String.Empty), html.ViewContext.HttpContext);
                        printView.InnerHtml += "Details".GetGlobalResource();
                        printView.Attributes.Add("href", printHref);

                        var pullLeftSpan = new TagBuilder("span");
                        pullLeftSpan.AddCssClass("pull-right");
                        pullLeftSpan.InnerHtml = canRead ? printView.ToString() : printView.InnerHtml;
                        row.InnerHtml += new TagBuilder("td") { InnerHtml = pullLeftSpan.ToString() };
                    }

                    table.InnerHtml += row.ToString();
                }
            }
            if (isAjax && !readOnly)
                foreach (var editId in itemEditModalIds)
                {
                    tableContainer.InnerHtml += BootstrapModal(editId);
                }

            tableContainer.InnerHtml += table.ToString();
            if (page)
            {
                tableContainer.InnerHtml += html.Pager(new { @class = "pull-right" }, new { @class = "page-count pull-right" }, new { @class = "pagination" }, updateID: (isAjax ? "#" + id : null), url: urlHelper.Action("index", controllerName), count: count, appendQueryString: sortQueryString);
                var clearBothdiv = new TagBuilder("div");
                clearBothdiv.Attributes.Add("style", "clear: both;");
                tableContainer.InnerHtml += clearBothdiv.ToString();
            }
            if (updateIDQueryString.NotNull())
                return new MvcHtmlString(tableContainer.InnerHtml);
            else
                return new MvcHtmlString(tableContainer.ToString());
        }

        #endregion

        #region Localization

        public static MvcHtmlString LocalizedDisplayNameFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression)
        {
            var memberExpression = (expression.Body as MemberExpression);
            var resourceProvider = Joe.Business.Resource.ResourceProvider.ProviderInstance;
            var isAdmin = html.ViewContext.HttpContext.User.IsInRole(Configuration.ConfigurationHelper.AdminRole);

            if (memberExpression != null)
            {
                var displayAttribute = (expression.Body as MemberExpression).Member.GetCustomAttributes(typeof(DisplayAttribute), true).SingleOrDefault();
                if (displayAttribute == null)
                {
                    var resource = resourceProvider.GetResource(memberExpression.Member.Name, memberExpression.Member.DeclaringType.Name);
                    var editLinkContainer = new TagBuilder("span");
                    var editLink = new TagBuilder("a");
                    var editIcon = new TagBuilder("i");
                    editIcon.AddCssClass("icon-edit");
                    editLink.InnerHtml = editIcon.ToString();
                    Boolean hasResource = resource != memberExpression.Member.Name;

                    return new MvcHtmlString(resource + GenerateResourceLink(hasResource, html, memberExpression.Member.Name, memberExpression.Member.DeclaringType.Name));

                }
            }

            return html.DisplayNameFor(expression);

        }

        public static MvcHtmlString LocalizedControllerHeader(this HtmlHelper html, Type controllerType = null)
        {
            controllerType = controllerType ?? html.ViewContext.Controller.GetType();
            var controllerNamespace = controllerType.Namespace.Replace(".", String.Empty);
            var controllerDisplayAttribute = controllerType.GetCustomAttributes(typeof(ControllerDisplayAttribute), true).SingleOrDefault() as ControllerDisplayAttribute;
            var resource = Joe.Business.Resource.ResourceProvider.ProviderInstance.GetResource(controllerType.Name, controllerNamespace);
            String header = null;
            Boolean hasResource = false;

            if (resource != controllerType.Name)
            {
                header = resource;
                hasResource = true;
            }
            else if (controllerDisplayAttribute.NotNull())
                header = controllerDisplayAttribute.Name;
            else
                header = controllerType.Name.Replace("Controller", String.Empty);

            return new MvcHtmlString(header + GenerateResourceLink(hasResource, html, controllerType.Name, controllerNamespace));
        }

        public static String LocalizedControllerName(this HtmlHelper html, Type controllerType = null)
        {
            controllerType = controllerType ?? html.ViewContext.Controller.GetType();
            var controllerNamespace = controllerType.Namespace.Replace(".", String.Empty);
            var controllerDisplayAttribute = controllerType.GetCustomAttributes(typeof(ControllerDisplayAttribute), true).SingleOrDefault() as ControllerDisplayAttribute;
            var resource = Joe.Business.Resource.ResourceProvider.ProviderInstance.GetResource(controllerType.Name, controllerNamespace);
            String header = null;

            if (resource != controllerType.Name)
            {
                header = resource;
            }
            else if (controllerDisplayAttribute.NotNull())
                header = controllerDisplayAttribute.Name;
            else
                header = controllerType.Name.Replace("Controller", String.Empty);

            return header;
        }

        public static MvcHtmlString LocalizedLabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, Object htmlAttributes = null)
        {
            var memberExpression = (expression.Body as MemberExpression);
            var resourceProvider = Joe.Business.Resource.ResourceProvider.ProviderInstance;
            var htmlRouteValueCollection = new RouteValueDictionary(htmlAttributes);
            var isAdmin = html.ViewContext.HttpContext.User.IsInRole(Configuration.ConfigurationHelper.AdminRole);
            if (memberExpression != null)
            {
                htmlRouteValueCollection.Add("for", memberExpression.Member.Name);
                var displayAttribute = (expression.Body as MemberExpression).Member.GetCustomAttributes(typeof(DisplayAttribute), true).SingleOrDefault();

                if (displayAttribute == null)
                {
                    var resource = resourceProvider.GetResource(memberExpression.Member.Name, memberExpression.Member.DeclaringType.Name);
                    var editLinkContainer = new TagBuilder("span");
                    var editLink = new TagBuilder("a");
                    var editIcon = new TagBuilder("i");
                    editIcon.AddCssClass("icon-edit");
                    editLink.InnerHtml = editIcon.ToString();

                    var label = new TagBuilder("label");
                    label.MergeAttributes(htmlRouteValueCollection);
                    Boolean hasResource = resource != memberExpression.Member.Name;

                    label.InnerHtml = resource + GenerateResourceLink(hasResource, html, memberExpression.Member.Name, memberExpression.Member.DeclaringType.Name);
                    return new MvcHtmlString(label.ToString());
                }
            }

            return html.LabelFor(expression, htmlAttributes);

        }

        private static String GenerateResourceLink(Boolean hasResource, HtmlHelper html, String name, String type)
        {
            var isAdmin = html.ViewContext.HttpContext.User.IsInRole(Configuration.ConfigurationHelper.AdminRole);
            var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
            var editContainer = new TagBuilder("span");
            var editLink = new TagBuilder("a");
            var editIcon = new TagBuilder("i");
            editIcon.AddCssClass("glyphicon glyphicon-edit");
            editLink.InnerHtml = editIcon.ToString();
            if (hasResource)
            {
                var editLinkHref = UrlHelper.GenerateContentUrl(String.Format("~/" + Configuration.ConfigurationHelper.AdminArea + "resource/edit/{0}/{1}/{2}", name, culture, type), html.ViewContext.HttpContext);
                editLink.Attributes.Add("href", editLinkHref);
            }
            else
            {
                var editLinkHref = UrlHelper.GenerateContentUrl(String.Format("~/" + Configuration.ConfigurationHelper.AdminArea + "resource/create?set=Name:{0}:Culture:{1}:Type:{2}", name, culture, type), html.ViewContext.HttpContext);
                editLink.Attributes.Add("href", editLinkHref);
            }

            editContainer.InnerHtml = editLink.ToString();
            return isAdmin ? " " + editContainer.ToString() : String.Empty;
        }

        private static IEnumerable<Type> LoadedControllerTypes { get; set; }

        private static Type GetControllerTypeByName(String controllerName, String nameSpace)
        {
            LoadedControllerTypes = LoadedControllerTypes ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(assem => assem.GetTypes()).Where(type => typeof(Controller).IsAssignableFrom(type));

            return LoadedControllerTypes.FirstOrDefault(type => type.Namespace == nameSpace && type.Name.ToLower() == controllerName.ToLower());
        }

        public static String GetGlobalResource(this String name)
        {
            var resourceProvider = Joe.Business.Resource.ResourceProvider.ProviderInstance;
            return resourceProvider.GetResource(name, "Global");
        }

        #endregion

        public static SelectList ToSelectList<TEnum>(this TEnum enumObj)
            where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            var values = from TEnum e in Enum.GetValues(typeof(TEnum))
                         select new { Id = e, Name = e.ToString() };
            return new SelectList(values, "Id", "Name", enumObj);
        }

        public static String ToRoute(this IEnumerable routeList)
        {
            String route = null;
            foreach (var routeValue in routeList)
            {
                if (route == null)
                    route = routeValue.ToString();
                else
                    route += "/" + routeValue.ToString();
            }
            return route;
        }

        public static String BuildSetQueryString(String[] keyProperties, Object[] keyValues)
        {
            String setValue = null;
            var count = 0;
            foreach (var keyProperty in keyProperties)
            {
                if (setValue.NotNull())
                    setValue += ":" + keyProperty + ":" + keyValues[count];
                else
                    setValue = keyProperty + ":" + keyValues[count];

                count++;
            }
            return setValue;
        }

        public static String BuildFilterColumnsQueryString(String[] filterColumns)
        {
            String filterValue = null;
            foreach (var filtercolumn in filterColumns)
            {
                if (filterValue.NotNull())
                    filterValue += "," + filtercolumn;
                else
                    filterValue = filtercolumn;
            }
            return filterValue;
        }

        public static MvcHtmlString ControllerMenu(this HtmlHelper html, String nameSpace, Object htmlAttribute = null)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes());
            var keyPairAttributes = new RouteValueDictionary(htmlAttribute);
            keyPairAttributes.CleanDataAttributes();
            var currentController = html.ViewContext.Controller.GetType().Name;
            var controllers = types.Where(type => type.Namespace == nameSpace
                                            && typeof(Controller).IsAssignableFrom(type)
                                            && !type.IsAbstract);

            var ul = new TagBuilder("ul");
            ul.MergeAttributes(keyPairAttributes);

            foreach (var controller in controllers)
            {
                var controllerDisplayAttribute = controller.GetCustomAttributes(typeof(ControllerDisplayAttribute), true).SingleOrDefault() as ControllerDisplayAttribute;
                var controllerLink = controller.Name.Replace("Controller", String.Empty);
                var controllerNamespace = controller.Namespace.Replace(".", String.Empty);
                if (controllerDisplayAttribute == null || !controllerDisplayAttribute.Hide)
                {
                    var isCurrnentController = controller.Name == currentController;
                    String resource = null;
                    bool hasResource = false;

                    var resourceProvider = Joe.Business.Resource.ResourceProvider.ProviderInstance;
                    resource = resourceProvider.GetResource(controller.Name, controllerNamespace);
                    if (resource != controller.Name)
                        hasResource = true;

                    var controllerName = hasResource ? resource : controllerDisplayAttribute.NotNull() ? controllerDisplayAttribute.Name : controller.Name.Replace("Controller", String.Empty);
                    var li = new TagBuilder("li");
                    if (isCurrnentController)
                        li.AddCssClass("active");
                    var actionLink = html.ActionLink(controllerName, "Index", controllerLink);
                    li.InnerHtml = actionLink.ToString() + GenerateResourceLink(hasResource, html, controller.Name, controllerNamespace);
                    ul.InnerHtml += li.ToString();
                }
            }

            return new MvcHtmlString(ul.ToString());
        }

        private static String GetAreaPath(this HtmlHelper html)
        {
            String area = html.ViewContext.RouteData.DataTokens["area"] as String;
            area = area.NotNull() ? area + "/" : string.Empty;
            return area;
        }

        private static TagBuilder BootstrapModal(String modalID, String innerHtml = null)
        {
            var modalContainer = new TagBuilder("div");
            modalContainer.AddCssClass("modal fade");
            modalContainer.Attributes.Add("role", "dialog");
            modalContainer.Attributes.Add("aria-hidden", "true");

            var modalDialog = new TagBuilder("div");
            modalDialog.AddCssClass("modal-dialog");

            var modalContent = new TagBuilder("div");
            modalContent.AddCssClass("modal-content");

            var createContainer = new TagBuilder("div");
            createContainer.AddCssClass("modal-body");
            modalContent.InnerHtml = createContainer.ToString();
            modalDialog.InnerHtml = modalContent.ToString();
            modalContainer.InnerHtml = modalDialog.ToString();

            modalContainer.Attributes.Add("id", modalID);

            return modalContainer;
        }

    }
}