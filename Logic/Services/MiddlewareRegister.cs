using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Logic;
using Logic.Helpers;
using Logic.IHelpers;
using Logic.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;


public static class MiddlewareRegister
{
	public static IServiceCollection RegisterHelpers(this IServiceCollection services)
	{
		services.AddScoped<IUserHelper, UserHelper>();
		services.AddScoped<IEmailService, EmailService>();
		services.AddScoped<IEmailTemplateService, EmailTemplateService>();
		services.AddScoped<IProjectHelper, ProjectHelper>();
		services.AddScoped<IProjectFeeHelper, ProjectFeeHelper>();
		services.AddScoped<IDropdownHelper, DropdownHelper>();
		services.AddScoped<SafeHavenAccountConsolidationService>();
		services.AddMemoryCache();

		services.AddHttpClient("SafeHaven");

		services.AddSingleton<ISafeHavenTokenService, SafeHavenTokenService>();

		services.AddHttpClient<ISafeHavenHelper, SafeHavenHelper>();

		services.AddSignalR();

		return services;
	}
	public class MyAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var user = context.GetHttpContext().User;
            return user != null && user.Identity.IsAuthenticated && user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        }
    }

	public static IApplicationBuilder UseHangfireConfiguration(this IApplicationBuilder app)
	{
		var options = new BackgroundJobServerOptions
		{
			ServerName = $"{Environment.MachineName}.{Guid.NewGuid()}"
		};

		app.UseHangfireServer(options);

		var robotStorage = new SqlServerStorage(app.ApplicationServices.GetService<IConfiguration>().GetConnectionString("DBConnectionString"), new SqlServerStorageOptions
		{
			SchemaName = "hangfire",
			JobExpirationCheckInterval = TimeSpan.FromHours(1)
		});

		JobStorage.Current = robotStorage;
		var dashboardOptions = new DashboardOptions
		{
			Authorization = [new MyAuthorizationFilter()]
		};
		AppHttpContext.Services = app.ApplicationServices;
		app.UseHangfireDashboard("/ApparusHangFire", options: dashboardOptions, storage: robotStorage);

		return app;
	}
}

