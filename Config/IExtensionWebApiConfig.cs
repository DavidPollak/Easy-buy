using System.Collections.Generic;
using System.Web.Http;

namespace NCR.RetailGateway.Services.Config
{
    public interface IExtensionWebApiConfig 
    {
        void ConfigureRoute(HttpRouteCollection routes, List<WebApiConfig.SensitiveRequestIdentifier> sensativeRequests);
    }
}
