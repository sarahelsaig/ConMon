using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace ConMon.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public List<string> AllowedClients { get; set; }

        public HangfireAuthorizationFilter(IConfiguration configuration)
        {
            AllowedClients = configuration.GetSection(nameof(AllowedClients)).GetChildren().Select(x => x.Value).ToList();
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Allow all authenticated users to see the Dashboard (potentially dangerous).
            //return httpContext.User.Identity.IsAuthenticated;
            //var allowed = AllowedClients.Contains(context.Request.RemoteIpAddress);
            //if (!allowed) context.Response.WriteAsync(context.Request.RemoteIpAddress).Wait();
            //return allowed;

            return true;
        }
    }
}
