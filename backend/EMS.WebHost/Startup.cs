﻿using System.IdentityModel.Tokens.Jwt;
using System.IO;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using EMS.WebHost.Helpers;

namespace EMS.WebHost
{
    public class WebConfig
    {
        public ushort Port { get; set; }
        public JwtConfig Jwt { get; set; }
    }

    public class Startup
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public WebConfig WebConfig { get; set; }
        private ILogger Logger { get; set; }

        public IWebHostEnvironment Env => _env;
        public IConfiguration Configuration => _configuration;

        static Startup()
        {
            // don't map any claims.. we don't need old style xml schema claims...
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        }

        public Startup( IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            _configuration = configuration;            
        }

        public void ConfigureServices(IServiceCollection services)
        {
            WebConfig wc = new ();
            Configuration.GetSection("web").Bind(wc);
            WebConfig = wc;

            // not nice to create the service here
            // and we also need a reference to the service later
            // hence we did add it as a dummy arg to configure....
            // TODO: fix this weird dependency
            IJWTService jwtCreator = null;            
            services.AddSingleton<IJWTService>((x) => {
                jwtCreator = ActivatorUtilities.CreateInstance<JwtTokenService>(x);
                return jwtCreator;
            });            

            services.AddControllers();

            services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {                
                options.TokenValidationParameters = jwtCreator.GetTokenValidationParameters();
                options.SaveToken = true;
            });

            //In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "dist";
            });
        }

        public void Configure(ILogger<Startup> logger, IApplicationBuilder app, IJWTService t /*see comment above*/)
        {
            Logger = logger;
            if (Env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        context.Response.StatusCode = 500;
                        context.Response.ContentType = "text/html";

                        await context.Response.WriteAsync("<html lang=\"en\"><body>\r\n");
                        await context.Response.WriteAsync("ERROR!<br><br>\r\n");

                        var exceptionHandlerPathFeature =
                            context.Features.Get<IExceptionHandlerPathFeature>();

                        if (exceptionHandlerPathFeature?.Error is FileNotFoundException)
                        {
                            await context.Response.WriteAsync("File error thrown!<br><br>\r\n");
                        }

                        await context.Response.WriteAsync("<a href=\"/\">Home</a><br>\r\n");
                        await context.Response.WriteAsync("</body></html>\r\n");
                        await context.Response.WriteAsync(new string(' ', 512));
                    });
                });
                app.UseHsts();
            }

            if (!Env.IsDevelopment())
                app.UseHttpsRedirection();

            app.UseMiddleware<Middleware.SecurityHeaders>();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseMiddleware<Middleware.SpaMiddleware>(Logger);

            app.UseRouting();

            // put this between UseRouting and UseEndpoints
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSpa(spa =>
            {
                if (Env.IsDevelopment())
                {
                    // Make sure you have started the frontend with npm run dev on port 5010
                    spa.UseProxyToSpaDevelopmentServer("http://localhost:5010");                //NOSONAR
                }                
            });

            app.Use((context, next) => {
                Logger.LogInformation($"{context.Request.Path}");
                return next.Invoke();
            });
        } 
    }
}
