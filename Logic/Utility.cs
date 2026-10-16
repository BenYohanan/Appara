using Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text;

namespace Logic
{
    public static class AppHttpContext
    {
        static IServiceProvider services = null;

        /// <summary>
        /// Provides static access to the framework's services provider
        /// </summary>
        public static IServiceProvider Services
        {
            get { return services; }
            set
            {
                if (services != null)
                {
                    throw new Exception("Can't set once a value has already been set.");
                }
                services = value;
            }
        }

        /// <summary>
        /// Provides static access to the current HttpContext
        /// </summary>
        public static HttpContext Current
        {
            get
            {
                IHttpContextAccessor httpContextAccessor = services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
                return httpContextAccessor?.HttpContext;
            }
        }

        public class EnsureGarageSettingIsLoadedAttribute : ActionFilterAttribute
        {
            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                HttpContext ctx = AppHttpContext.Current;
                if (Current.Session.GetString("loggedInUser") == null)
                {
                    filterContext.Result = new RedirectResult("~/Account/Login");
                    return;
                }
                base.OnActionExecuting(filterContext);
            }
        }
    }
    public class Utility
    {
        public static ApplicationUser GetCurrentUser()
        {
            var loggInUser = new ApplicationUser();
            var userString = AppHttpContext.Current?.Session?.GetString("loggedInUser");
            if (userString != null)
            {
                loggInUser = JsonConvert.DeserializeObject<ApplicationUser>(userString);
                return loggInUser;
            }
            var userName = AppHttpContext.Current?.User?.Claims?.Where(x => x.Type == ClaimTypes.Name).FirstOrDefault()?.Value;
            loggInUser.UserName = userName;
            return loggInUser;
        }

    }
}
