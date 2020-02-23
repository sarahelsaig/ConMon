using ConMon.Classes;
using ConMon.Models;
using ConMon.Models.Scheduler;
using ConMon.Services;
using Hangfire;
using Hangfire.MySql.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

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

        private Action<DbContextOptionsBuilder> CreateDbConnection(string connectionString) =>
            new Action<DbContextOptionsBuilder>(options =>
            {
                switch (Configuration.GetConnectionType())
                {
                    case ConnectionType.SqlServer: options.UseSqlServer(connectionString); break;
                    case ConnectionType.MySql: options.UseMySql(connectionString); break;
                    case ConnectionType unknown: throw new InvalidOperationException($"Unknown or unsupported connection type: {unknown}");
                }
            });

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var schedulerConnectionString = Configuration.GetConnectionString("Scheduler");


            services.AddSingleton(Configuration);
            services.AddSingleton(ProgramAlias.DictionaryFromConfiguration(Configuration));
            services.AddScoped<IApplicationService, ApplicationService>();
            services.AddScoped<IRunAs, RunAs>();


            services.AddHangfire(configuration => {
                switch (Configuration.GetConnectionType())
                {
                    case ConnectionType.SqlServer: configuration.UseSqlServerStorage(schedulerConnectionString); break;
                    case ConnectionType.MySql: configuration.UseStorage(new MySqlStorage(schedulerConnectionString)); break;
                    case ConnectionType unknown: throw new InvalidOperationException($"Unknown or unsupported connection type: {unknown}");
                }
            });
            services.AddDbContext<SchedulerContext>(CreateDbConnection(schedulerConnectionString));
            
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            if (IsDebug)
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHangfireServer();
            app.UseHangfireDashboard(options: new DashboardOptions
            {
                AppPath = "/",
                Authorization = new[] { new Filters.HangfireAuthorizationFilter() },
            });

            app.UseDefaultFiles();
            app.UseStaticFiles();
            //app.UseHttpsRedirection();
            app.UseRouting();
            //app.UseAuthentication();
            //app.UseAuthorization();
            app.UseEndpoints(endpoints => 
            {
                endpoints.MapControllers();
                //endpoints.MapRazorPages();
                //endpoints.MapHub<MyChatHub>();
                //endpoints.MapGrpcService<MyCalculatorService>();
            });

            using var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
            using (var context = scope.ServiceProvider.GetService<SchedulerContext>()) context.Database.Migrate();
        }
    }
}
