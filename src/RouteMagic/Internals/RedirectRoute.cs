using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Routing;
using RouteMagic.HttpHandlers;

namespace RouteMagic.Internals
{
    // Craziness! In this case, there's no reason the route can't be its own route handler.
    public class RedirectRoute : RouteBase, IRouteHandler
    {
        public RedirectRoute(RouteBase sourceRoute, RouteBase targetRoute, bool permanent)
            : this(sourceRoute, targetRoute, permanent, null)
        {
        }

        public RedirectRoute(RouteBase sourceRoute, RouteBase targetRoute, bool permanent, RouteValueDictionary additionalRouteValues)
        {
            SourceRoute = sourceRoute;
            TargetRoute = targetRoute;
            Permanent = permanent;
            AdditionalRouteValues = additionalRouteValues;
        }

        public RouteBase SourceRoute
        {
            get;
            set;
        }

        public RouteBase TargetRoute
        {
            get;
            set;
        }

        public bool Permanent
        {
            get;
            set;
        }

        public bool IncludeQueryStringInRedirect { get; set; }

        public RouteValueDictionary AdditionalRouteValues
        {
            get;
            private set;
        }

        public override RouteData GetRouteData(HttpContextBase httpContext)
        {
            // Use the original route to match
            var routeData = SourceRoute.GetRouteData(httpContext);
            if (routeData == null)
            {
                return null;
            }
            // But swap its route handler with our own
            routeData.RouteHandler = this;
            return routeData;
        }

        public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
        {
            // Redirect routes never generate an URL.
            return null;
        }

        public RedirectRoute To(RouteBase targetRoute)
        {
            return To(targetRoute, null);
        }

        public RedirectRoute To(RouteBase targetRoute, object routeValues)
        {
            return To(targetRoute, new RouteValueDictionary(routeValues));
        }

        public RedirectRoute To(RouteBase targetRoute, RouteValueDictionary routeValues)
        {
            if (targetRoute == null)
            {
                throw new ArgumentNullException("targetRoute");
            }

            // Set once only
            if (TargetRoute != null)
            {
                throw new InvalidOperationException("TargetRoute should be set once only");
            }
            TargetRoute = targetRoute;

            // Set once only
            if (AdditionalRouteValues != null)
            {
                throw new InvalidOperationException("AdditionalRouteValues should be set once only");
            }
            AdditionalRouteValues = routeValues;

            return this;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            var requestRouteValues = requestContext.RouteData.Values;
            var routeValues = AdditionalRouteValues.Merge(requestRouteValues);
            var vpd = TargetRoute.GetVirtualPath(requestContext, routeValues);

            if (vpd == null)
            {
                return new DelegateHttpHandler(rc => rc.HttpContext.Response.StatusCode = 404, requestContext.RouteData, false);
            }

            var targetUrl = "~/" + vpd.VirtualPath;

            if (!IncludeQueryStringInRedirect)
            {
                return new RedirectHttpHandler(targetUrl, Permanent, isReusable: false);
            }

            var qs = new StringBuilder("?");
            var queryString = requestContext.HttpContext.Request.QueryString;

            foreach (var key in queryString.AllKeys.Where(key => !requestRouteValues.ContainsKey(key)))
            {
                qs.AppendFormat("{0}={1}&", key, queryString[key]);
            }

            targetUrl += qs.ToString(0, qs.Length - 1);

            return new RedirectHttpHandler(targetUrl, Permanent, isReusable: false);
        }

    }
}
