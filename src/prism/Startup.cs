using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Prism.Middleware;

namespace Prism
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddOptions<RequestLogOptions>()
                .ValidateDataAnnotations();
            services.Configure<RequestLogOptions>(Configuration.GetSection("RequestLog"));

            services.AddHttpContextAccessor();
            services.AddConnectionInfoFactory();

            services.AddSingleton<RequestLog>();
            services.AddSingleton<UriForwardingTransformer>();
            services.AddSingleton<AuthProvider>();

            // just playin around with data produced in a middletier getting into other layers
            services.AddScoped<TrackedRequestAccessor>();

            services
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddRazorPagesOptions(options =>
                {
                    options.AllowAreas = true;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseMvc();
            app.Map(
                "/admin",
                mapApp =>
                {
                    mapApp.UseMvc();
                });

            app.MapWhen(context => context.Request.Path.HasValue && context.Request.Path.StartsWithSegments(new PathString("/uri"),StringComparison.OrdinalIgnoreCase), mapApp =>
            {
                //http://localhost:32775/uri/pkgs.dev.azure.com/codesharing-SU0/_packaging/zackrun1/nuget/v3/index.json
                mapApp.UseLoggingMiddleware();
                mapApp.RunForwardingMiddleware();
            });
        }
    }
}
/*
 * usemvc(
 /*routes =>
                    {
                        routes.MapRoute("areaRoute", "{area:exists}/{controller=Admin}/{action=Index}");

                        routes.MapRoute(
                            name: "default",
                            template: "{area=Admin}/{controller=Diagnostics}/{action=Index}");
                            
                    }
 **/
