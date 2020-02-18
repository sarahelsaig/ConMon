using ConMon.Services;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConMon
{
    public class Startup
    {
        public static bool IsDebug { get; } =
#if DEBUG
            true;
#else
            false;
#endif

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var schedulerConnectionString = Configuration.GetConnectionString("Scheduler");
            Services.ApplicationService.ConnectionString = schedulerConnectionString;
            services.AddHangfire(configuration => {
                configuration.UseSqlServerStorage(schedulerConnectionString);
            });
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (IsDebug)
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            ApplicationService.RunAsDomain = Configuration.GetSection("RunAs").GetValue<string>("Domain") ?? "";
            ApplicationService.RunAsUser = Configuration.GetSection("RunAs").GetValue<string>("User");
            ApplicationService.RunAsPass = Configuration.GetSection("RunAs").GetValue<string>("Pass");

            Controllers.ScheduleController.ProgramAliases = Classes.ProgramAlias.Config(Configuration);

            app.UseHangfireServer();
            app.UseHangfireDashboard(options: new DashboardOptions
            {
                AppPath = "/",
                Authorization = new[] { new Filters.HangfireAuthorizationFilter() },
            });

            app.UseHttpsRedirection();
            app.UseMvc();
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }
    }
}
