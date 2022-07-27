using System.Web.Mvc;
using System.Web.Routing;

namespace NCR.RetailGateway.Services.Config
{
    /// <summary>
    /// HTTP Route mapping
    /// </summary>
    public class RouteConfig
    {
        /// <summary>
        /// Register Routes
        /// </summary>
        /// <param name="routes"></param>
        public static void RegisterRoutes(RouteCollection routes)
        {

            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Default - Go to Home Controller (Documentation)
            routes.MapRoute(name: "Default", url: RestApiConstants.FullPrefix + "{controller}/{action}/{id}", defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        }
    }
}
