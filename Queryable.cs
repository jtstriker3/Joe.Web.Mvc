using System;
using System.Data.Objects;
using System.Net;
using System.Runtime.Serialization;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Collections.Specialized;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Http;
using Joe.Web.Mvc.Utility.Extensions;
using Joe.Web.Mvc.Utility.Configuration;
using Joe.Map;


namespace Joe.Web.Mvc
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class JoeQueryableAttribute : QueryableAttribute, IOrderedFilter
    {
        public int Order { get; private set; }

        public JoeQueryableAttribute()
        {
            Order = 999;
        }

        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            try
            {
                base.OnActionExecuted(actionExecutedContext);

                if (actionExecutedContext.Response.IsSuccessStatusCode)
                {

                    IQueryable iquery = ((ObjectContent)actionExecutedContext.Response.Content).Value as IQueryable;
                    //This is super dirty but lets you hit the database before applying odata filters. This is to get around
                    //Terdata.Net Provider Issues.
                    if (Convert.ToBoolean(actionExecutedContext.Request.RequestUri.ParseQueryString()["InvokeBeforeOdata"]))
                        iquery = this.ToList(iquery);

                    ODataQueryParser queryParser = new ODataQueryParser(actionExecutedContext.Request.RequestUri, iquery);
                    Delegate lambdaQuery = queryParser.BuildODataExpression().Compile();
                    Delegate lambdaInlineCount = queryParser.InlineCountExpression().Compile();
                    var result = (IEnumerable)lambdaQuery.DynamicInvoke();
                    var orginalCount = (int)lambdaInlineCount.DynamicInvoke();
                    actionExecutedContext.Response.Headers.Add("X-Total-Count", orginalCount.ToString());

                    if (queryParser.HasGroupBy)
                    {
                        actionExecutedContext.Response = actionExecutedContext.Request.CreateResponse(HttpStatusCode.OK, result);
                        actionExecutedContext.Response.Headers.Add("X-Total-Count", orginalCount.ToString());
                    }
                    else
                        ((ObjectContent)actionExecutedContext.Response.Content).Value = result;
                }
            }
            catch (Exception ex)
            {
                ex.LogError();
                actionExecutedContext.SetErrorResponse(ex);
            }

        }

        private IQueryable ToList(IQueryable iquery)
        {
            var genericType = iquery.GetType().GetGenericArguments().Single();
            var toListExpression = Expression.Call(typeof(Enumerable), "ToList", new[] { genericType }, Expression.Constant(iquery));
            var list = (IEnumerable)Expression.Lambda(toListExpression).Compile().DynamicInvoke();
            return list.AsQueryable();
        }
    }

    class ODataQueryParser
    {
        private NameValueCollection _query { get; set; }
        private IQueryable _iquery { get; set; }
        private ParameterExpression _parameterExpression { get; set; }
        private static List<String> functionList = new List<string>()
        {"contains", "endswith", "startswith", "length", "indexof", "replace", "substring", "tolower", 
            "toupper", "trim", "concat", "day", "hour", "minute", "month",
            "second", "year", "round", "floor", "celing", "IsOf", "groupby" };
        private static List<String> operatorList = new List<string>(){
             " eq ", " ne ", " gt ", " ge ", " and ", " or ", " not ", " add ", " sub ", " mul ", " div ", " mod ", "("
         };

        public ODataQueryParser(Uri query, IQueryable iquery)
        {
            _query = query.ParseQueryString();
            _iquery = iquery;
            _parameterExpression = Expression.Parameter(_genericType);
        }

        public int _take
        {
            get
            {
                return Convert.ToInt32(_query["top"]);
            }
        }

        private int _skip
        {
            get
            {
                return Convert.ToInt32(_query["skip"]);
            }
        }

        private Boolean _hasTake
        {
            get
            {
                return !String.IsNullOrEmpty(_query["top"]);
            }
        }

        private Boolean _hasSkip
        {
            get
            {
                return !String.IsNullOrEmpty(_query["skip"]);
            }
        }

        private Boolean _hasWhere
        {
            get
            {
                return !String.IsNullOrWhiteSpace(_where);
            }
        }

        public Boolean HasGroupBy
        {
            get { return !String.IsNullOrEmpty(_groupby); }
        }

        private Type _queryType
        {
            get
            {
                return _iquery.GetType().GetGenericArguments()[0];
            }
        }

        private String _filter
        {
            get
            {
                return _query["joefilter"];
            }
        }

        private String _groupby
        {
            get
            {
                return _query["groupby"];
            }

        }

        private Type _genericType
        {
            get
            {
                return _iquery.GetType().GetGenericArguments()[0];
            }
        }

        private ODataFilter _odataFilter
        {
            get
            {
                return new ODataFilter(_filter, _parameterExpression, _genericType);
            }
        }

        private String _where
        {
            get
            {
                return _query["where"];
            }
        }

        private class ODataFilter
        {
            private String _filter { get; set; }
            private String _left { get; set; }
            private String _right { get; set; }
            private Boolean isOperator { get; set; }
            private string _operator { get; set; }
            private String _subOp { get; set; }
            private Expression _expression { get; set; }
            private Type _type { get; set; }
            private Queue<OdataSibling> SiblingFilters { get; set; }
            public ODataFilter(String filter, Expression start, Type t)
            {
                _filter = filter;
                _expression = start;
                _type = t;
                SiblingFilters = new Queue<OdataSibling>();
                _operator = parseOperator();
                parseSiblingFilters();
                if (!String.IsNullOrEmpty(_operator))
                    parseSides(_operator);
            }

            private ODataFilter Left
            {
                get
                {
                    return new ODataFilter(_left, _expression, _type);
                }
            }

            private ODataFilter Right
            {
                get
                {
                    return new ODataFilter(_right, _expression, _type);
                }
            }

            private void parseSiblingFilters()
            {
                if (!isOperator)
                {
                    Regex ex = new Regex(" or (?!((?![\\(\\)]).)*\\))| and (?!((?![\\(\\)]).)*\\))| eq (?!((?![\\(\\)]).)*\\))| ne (?!((?![\\(\\)]).)*\\))|  \\((?!((?![\\(\\)]).)*\\))");


                    var filters = ex.Split(_filter);
                    var matches = ex.Matches(_filter);


                    _filter = filters[0].Trim();
                    for (int i = 1; i < filters.Length; i++)
                    {

                        var op = matches[i - 1].Value.Trim();
                        OdataSibling sib = new OdataSibling(new ODataFilter(filters[i].Trim(), _expression, _type), op);
                        SiblingFilters.Enqueue(sib);
                    }

                }
            }

            private String parseOperator()
            {
                var function = functionList.Where(fl => _filter.ToLower().StartsWith(fl));
                string op = null;
                if (function.Count() > 1)
                {
                    if (_filter.ToLower().StartsWith("contains"))
                        op = "contains";
                    else
                        op = "substring";
                }
                else if (function.Count() == 1)
                    op = function.SingleOrDefault();
                else
                {
                    int current = int.MaxValue;
                    String operation = null;
                    foreach (String str in operatorList)
                    {
                        int index = _filter.ToLower().IndexOf(str);
                        if (index != -1 && index < current)
                        {
                            operation = str;
                            current = index;
                        }
                    }
                    isOperator = true;
                    if (!String.IsNullOrEmpty(operation))
                        op = operation.Trim();
                }

                return op;
            }

            private void parseSides(String op)
            {
                string Operator = op;
                if (Operator != null)
                {
                    if (isOperator)
                    {
                        if (Operator != "(")
                        {
                            var tempFilter = _filter.Replace(Operator, Operator.ToLower());
                            var leftAndRight = tempFilter.Split(new[] { Operator }, 2, StringSplitOptions.RemoveEmptyEntries);
                            _left = leftAndRight[0].Trim();
                            _right = leftAndRight[1].Trim();
                        }
                        else
                        {
                            var tempFilter = _filter.Remove(0, 1);
                            tempFilter = tempFilter.Remove(tempFilter.Length - 1);
                            _filter = tempFilter;
                            _subOp = parseOperator();
                            //parseSiblingFilters(true);
                            if (!String.IsNullOrEmpty(_operator))
                                parseSides(_subOp);
                        }
                    }
                    else
                    {
                        var fullOp = _operator + "(";
                        var filterRemovedFunction = _filter.Remove(_filter.IndexOf(fullOp), fullOp.Length);
                        //var leftAndRight = filterRemovedFunction.Split(new[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        Regex ex = new Regex(",(?!((?![\'\']).)*\')");
                        var leftAndRight = ex.Split(filterRemovedFunction);

                        _left = leftAndRight[0].Trim();
                        _right = leftAndRight[1].Trim();
                        _right = _right.Remove(_right.IndexOf(')'), 1);
                    }
                }
            }

            public Expression BuildExpression()
            {
                Expression ex = null;
                if (String.IsNullOrEmpty(_operator))
                {
                    if (_filter.StartsWith("'") && _filter.EndsWith("'"))
                    {
                        ex = Expression.Constant(_filter.Remove(0, 1).Remove(_filter.Length - 2, 1));
                    }
                    else if (new[] { "true", "false", "null" }.Contains(_filter))
                    {
                        if (_filter != "null")
                            ex = Expression.Constant(Convert.ToBoolean(_filter));
                        else
                            ex = Expression.Constant(null);
                    }
                    else
                    {
                        var tempFilter = _filter.Replace('/', '.');
                        PropertyInfo info;
                        String suffix = null;
                        if (tempFilter.Contains('-'))
                        {
                            var splitFilter = tempFilter.Split('-');
                            info = Reflection.ReflectionHelper.GetEvalPropertyInfo(_type,
                                                                                            splitFilter.Length ==
                                                                                            2
                                                                                                ? splitFilter[0]
                                                                                                : tempFilter);
                            suffix = splitFilter[1];

                        }
                        else
                        {
                            info = Reflection.ReflectionHelper.GetEvalPropertyInfo(_type, tempFilter);
                        }

                        if (typeof(IEnumerable).IsAssignableFrom(info.PropertyType) && !typeof(String).IsAssignableFrom(info.PropertyType))
                        {
                            ex = Expression.Property(_expression, info);
                            var listType = info.PropertyType.GetGenericArguments()[0];
                            if (listType.IsPrimitive || typeof(String).IsAssignableFrom(listType))
                                ex = Expression.Property(_expression, info);
                            else
                            {
                                var parameterExpression = Expression.Parameter(listType, listType.Name.ToLower());
                                var propExpression = Expression.Property(parameterExpression,
                                    String.IsNullOrEmpty(suffix) ? info :
                                                                         Reflection.ReflectionHelper.
                                                                             GetEvalPropertyInfo(listType, suffix));
                                var lambdaExpression = Expression.Lambda(propExpression, new[] { parameterExpression });
                                ex = Expression.Call(typeof(Enumerable), "Select", new[] { listType, propExpression.Type }, ex, lambdaExpression);
                            }
                        }
                        else
                            ex = Expression.Property(_expression, info);
                    }

                }
                else if (isOperator)
                {
                    //switch case on operator build left and right into expression
                    ex = BuildOperatorExpression(_operator, Left.BuildExpression(), Right.BuildExpression(), false);

                }
                else
                {
                    switch (_operator)
                    {

                        case "contains":
                            Expression leftExpression = Left.BuildExpression();
                            Expression rightExpression = Right.BuildExpression();
                            if (typeof(IEnumerable).IsAssignableFrom(rightExpression.Type) && !typeof(String).IsAssignableFrom(rightExpression.Type))
                            {

                                var genericType = rightExpression.Type.GetGenericArguments()[0];
                                var value = Convert.ChangeType(((ConstantExpression)leftExpression).Value, genericType);
                                var constant = Expression.Constant(value);
                                ex = Expression.Call(typeof(Enumerable), "Contains", new[] { genericType }, rightExpression, constant);
                            }
                            else
                                ex = Expression.Call(rightExpression, rightExpression.Type.GetMethod("Contains"), leftExpression);
                            break;
                    }

                }
                if (ex != null)
                    return BuildSiblingExpressions(ex);
                else
                    throw new Exception(String.Format("Operation '{0}' not supported yet", _operator));
            }

            private Expression BuildSiblingExpressions(Expression left)
            {
                while (SiblingFilters.Count > 0)
                {
                    OdataSibling sib = SiblingFilters.Dequeue();
                    left = BuildOperatorExpression(sib.SiblingOperator, left, sib.Filter.BuildExpression(), false);

                } return left;
            }

            private Expression BuildOperatorExpression(String op, Expression left, Expression right, Boolean grouped)
            {
                Expression ex = null;
                switch (op)
                {

                    case "eq":
                        ex = Expression.Equal(left, right);
                        break;
                    case "ne":
                        ex = Expression.NotEqual(left, right);
                        break;
                    case "and":
                        if (grouped)
                            ex = Expression.AndAlso(left, right);
                        else
                            ex = Expression.And(left, right);
                        break;
                    case "or":
                        if (grouped)
                            ex = Expression.ExclusiveOr(left, right);
                        else
                            ex = Expression.Or(left, right);
                        break;
                    case "(":
                        ex = BuildOperatorExpression(_subOp, left, right, true);
                        break;
                    default:
                        throw new Exception(String.Format("Operation '{0}' not supported yet", op));
                }

                return ex;
            }

            public class OdataSibling
            {
                public ODataFilter Filter { get; set; }
                public String SiblingOperator { get; set; }

                public OdataSibling(ODataFilter filter, String siblingOperator)
                {
                    Filter = filter;
                    SiblingOperator = siblingOperator;
                }
            }
        }

        public LambdaExpression InlineCountExpression()
        {
            MethodInfo methInfo = typeof(Queryable).GetMethod("Count", new[] { typeof(IQueryable<>) });
            Expression ex = Expression.Call(typeof(Queryable), "Count", new[] { _queryType }, BuildFilterExpression());
            return Expression.Lambda(ex);
        }

        public LambdaExpression BuildODataExpression()
        {
            Expression ex = BuildFilterExpression();
            if (_hasSkip)
                ex = Expression.Call(typeof(Queryable), "Skip", new[] { _queryType }, ex, Expression.Constant(_skip));
            if (_take > 0 && _hasTake)
                ex = Expression.Call(typeof(Queryable), "Take", new[] { _queryType }, ex, Expression.Constant(_take));
            if (HasGroupBy)
                ex = BuildGroupByExpression(ex);

            return Expression.Lambda(ex);

        }

        private Expression BuildFilterExpression()
        {
            Expression ex = Expression.Constant(_iquery);
            if (!String.IsNullOrEmpty(_filter))
            {
                Expression filterEx = Expression.Lambda(_odataFilter.BuildExpression(), new ParameterExpression[] { _parameterExpression });
                ex = Expression.Call(typeof(Queryable), "Where", new[] { _queryType }, ex, filterEx);
            }
            if (this._hasWhere)
                ex = Expression.Call(typeof(FilterExtensions), "Filter", new[] { _queryType }, ex, Expression.Constant(_where), Expression.Constant(null, typeof(Object)));

            return ex;
        }

        private Expression BuildGroupByExpression(Expression ex)
        {
            var groupByInfo = _queryType.GetProperty(_groupby);
            var paramerterExpression = Expression.Parameter(_queryType);
            var propertyExpression = Expression.Property(paramerterExpression, groupByInfo);
            var lambdaExpression = Expression.Lambda(propertyExpression, new[] { paramerterExpression });
            ex = Expression.Call(typeof(Enumerable), "GroupBy", new[] { _queryType, groupByInfo.PropertyType }, ex, lambdaExpression);
            var igroupingType = typeof(IGrouping<,>).MakeGenericType(new[] { groupByInfo.PropertyType, _queryType });
            var groupingType = typeof(Grouping<,>).MakeGenericType(new[] { groupByInfo.PropertyType, _queryType });
            var iqueryableType = typeof(IQueryable<>).MakeGenericType(new[] { igroupingType });
            var groupingConstructror = groupingType.GetConstructor(new[] { igroupingType });
            var groupingParameterExpression = Expression.Parameter(igroupingType);
            var newGrouping = Expression.New(groupingConstructror, new[] { groupingParameterExpression });
            var newGroupingLambda = Expression.Lambda(newGrouping, new[] { groupingParameterExpression });
            ex = Expression.Call(typeof(Enumerable), "Select", new[] { igroupingType, groupingType }, ex, newGroupingLambda);
            return ex;
        }

    }
    [DataContract]
    public class Grouping<TKey, TElement>
    {
        [DataMember]
        public List<TElement> Elements { get; set; }

        public Grouping(IGrouping<TKey, TElement> grouping)
        {
            Key = grouping.Key;
            Elements = grouping.ToList();
        }

        [DataMember]
        public TKey Key { get; set; }
    }
}