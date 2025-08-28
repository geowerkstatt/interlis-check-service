using Geowerkstatt.Ilicop.Web.Ilitools;
using Geowerkstatt.Ilicop.Web.Services;
using Geowerkstatt.Interlis.RepositoryCrawler;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NetTopologySuite.IO.Converters;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geowerkstatt.Ilicop.Web
{
    public class Startup
    {
        private const int MaxRequestBodySize = 209715200;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the application name if set; otherwise, a predefined default.
        /// </summary>
        public string ApplicationName =>
            Configuration.GetValue<string>("CUSTOM_APP_NAME") ?? "INTERLIS Web-Check-Service";

        private static readonly string[] swaggerCustomOrder = new[] { "Upload", "Status", "Download", "Settings" };

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddHttpContextAccessor();
            services.AddHealthChecks().AddCheck<IlitoolsHealthCheck>("Ilivalidator");
            services.AddApiVersioning(config =>
            {
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
                config.ReportApiVersions = true;
                config.ApiVersionReader = new HeaderApiVersionReader("api-version");
            });
            services.AddCors(options =>
            {
                options.AddPolicy("CorsSettings", policy =>
                {
                    policy.AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .WithOrigins("https://localhost:44302");
                });
            });

            services.AddSingleton(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                return new IlitoolsEnvironment
                {
                    InstallationDir = cfg.GetValue<string>("ILITOOLS_HOME_DIR") ?? "/ilitools",
                    CacheDir = cfg.GetValue<string>("ILITOOLS_CACHE_DIR") ?? "/cache",
                    ModelRepositoryDir = cfg.GetValue<string>("ILITOOLS_MODEL_REPOSITORY_DIR") ?? "/repository",
                    PluginsDir = cfg.GetValue<string>("ILITOOLS_PLUGINS_DIR") ?? "/plugins",
                    EnableGpkgValidation = cfg.GetValue<bool>("ENABLE_GPKG_VALIDATION"),
                };
            });

            services.AddScoped(sp =>
            {
                var env = sp.GetRequiredService<IlitoolsEnvironment>();
                var httpClient = sp.GetRequiredService<HttpClient>();
                return RepositoryReaderFactory.Create(env.ModelRepositoryDir, httpClient);
            });

            services.AddHttpClient();
            services.AddHostedService<IlitoolsBootstrapService>();
            services.AddTransient<IlitoolsExecutor>();

            services.AddScoped<IProfileService, ProfileService>();
            services.AddSingleton<IValidatorService, ValidatorService>();
            services.AddHostedService(services => (ValidatorService)services.GetService<IValidatorService>());
            services.AddTransient<IValidator, Validator>();
            services.AddTransient<IFileProvider, PhysicalFileProvider>(x => new PhysicalFileProvider(x.GetRequiredService<IConfiguration>(), "ILICOP_UPLOADS_DIR"));
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                options.JsonSerializerOptions.Converters.Add(new GeoJsonConverterFactory());
            });
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = MaxRequestBodySize;
            });
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = MaxRequestBodySize;
            });
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = $"{ApplicationName} API Documentation",
                });

                // Include existing documentation in Swagger UI.
                options.IncludeXmlComments(
                    Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));

                // Custom order in Swagger UI.
                options.OrderActionsBy(apiDescription =>
                {
                    var controllerName = (apiDescription.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;
                    return $"{Array.IndexOf(swaggerCustomOrder, controllerName)}";
                });

                options.EnableAnnotations();
                options.SupportNonNullableReferenceTypes();
            });

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            // Setup logging
            loggerFactory.AddFile(Configuration.GetSection("Logging"));

            // By default Kestrel responds with a HTTP 400 if payload is too large.
            app.Use(async (context, next) =>
            {
                if (context.Request.ContentLength > MaxRequestBodySize)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    await context.Response.WriteAsync("Payload Too Large");
                    return;
                }

                await next.Invoke();
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");

                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseCors("CorsSettings");
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");
                endpoints.MapHealthChecks("/health");
            });

            app.UseSwagger(options =>
            {
                options.RouteTemplate = "api/{documentName}/swagger.json";
            });
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/api/v1/swagger.json", $"{ApplicationName} REST API v1");
                options.RoutePrefix = "api";
                options.DocumentTitle = $"{ApplicationName} API Documentation";
                options.InjectStylesheet("../swagger-ui.css");
                options.InjectJavascript("../swagger-ui.js");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseProxyToSpaDevelopmentServer("http://localhost:3000");
                }
            });
        }
    }
}
