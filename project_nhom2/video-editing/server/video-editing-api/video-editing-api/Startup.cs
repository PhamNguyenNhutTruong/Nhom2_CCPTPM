﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;
using video_editing_api.Model;
using video_editing_api.Model.Collection;
using video_editing_api.Service;
using video_editing_api.Service.DBConnection;
using video_editing_api.Service.Film;
using video_editing_api.Service.Storage;
using video_editing_api.Service.User;
using video_editing_api.Service.VideoEditing;
using static video_editing_api.Controllers.VideoEditingsController;

namespace video_editing_api
{
    public class Startup
    {
        private readonly string _myAllowSpecificOrigins = "AllowSpecficOrigins";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Đăng ký đối tượng trình giải mã tùy chỉnh của mình với MongoDB
            MongoDbInitializer.Initialize();
            services.AddHostedService<MergeQueueBackgroundService>();
            services.AddSignalR();
            services.AddHttpClient();
            #region AddCors
            services.AddCors(options =>
            {
                options.AddPolicy(name: _myAllowSpecificOrigins,
                    builder =>
                    {
                        builder
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetIsOriginAllowed(origin => true) // allow any origin
                        .AllowCredentials()
                        .WithExposedHeaders("Content-Disposition");
                    });
            });
            #endregion
            services.AddControllers();
            #region Add Identity
            var mongoDbSettings = Configuration.GetSection(nameof(DbConfig)).Get<DbConfig>();
            services.AddIdentity<AppUser, AppRole>()
                .AddMongoDbStores<AppUser, AppRole, Guid>(mongoDbSettings.ConnectionString, mongoDbSettings.DbName);
            #endregion

            services.Configure<DbConfig>(Configuration.GetSection(SystemConstants.DbConfig));
            #region Add Service
            services.AddScoped<IDbClient, DbClient>();
            services.AddScoped<IVideoEditingService, VideoEditingService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IStorageService, StorageService>();
            services.AddScoped<IFilmService, FilmService>();
            #endregion

            #region Add Authentication
            string issuer = Configuration.GetValue<string>("Tokens:Issuer");
            string signingKey = Configuration.GetValue<string>("Tokens:Key");
            //string apiKey = Configuration.GetValue<string>("Cloudinary:APIKey");
            //string apiSecret = Configuration.GetValue<string>("Cloudinary:APISecret");
            //string signingKey = $"{apiKey}:{apiSecret}";
            byte[] signingKeyBytes = System.Text.Encoding.UTF8.GetBytes(signingKey);
            var key = new SymmetricSecurityKey(signingKeyBytes);

            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            // .AddJwtBearer(opt =>
            // {
            //     opt.TokenValidationParameters = new TokenValidationParameters
            //     {
            //         ValidateIssuer = true,
            //         ValidateAudience = true,
            //         ValidateLifetime = true,
            //         ValidateIssuerSigningKey = true,
            //         ValidIssuer = Configuration["Tokens:Issuer"],
            //         ValidAudience = Configuration["Tokens:Audience"],
            //         IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Tokens:Key"])),
            //         ClockSkew = TimeSpan.Zero
            //     };
            // });
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    //ValidateIssuer = true,
                    //ValidateAudience = true,
                    //ValidateLifetime = true,
                    //ValidateIssuerSigningKey = true,
                    //ValidIssuer = Configuration["Tokens:Issuer"],
                    //ValidAudience = Configuration["Tokens:Audience"],
                    //IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Tokens:Key"])),
                    //ClockSkew = TimeSpan.Zero
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });

            //services.AddAuthorization(options =>
            //{
            //    options.AddPolicy("Viewer", policy => policy.RequireRole("Viewer"));
            //    options.AddPolicy("Uploader", policy => policy.RequireRole("Uploader"));
            //    options.AddPolicy("Creator", policy => policy.RequireRole("Creator"));
            //});
            #endregion
            #region Add Authorization
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Read", policy => policy.RequireRole("Read"));
                options.AddPolicy("Write", policy => policy.RequireRole("Write"));
                options.AddPolicy("Excute", policy => policy.RequireRole("Excute"));
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
                //options.AddPolicy("fullpermisson", policy =>
                //{
                //    policy.RequireAssertion(context =>
                //    context.User.IsInRole("Admin") ||
                //    context.User.IsInRole("Write") ||
                //    context.User.IsInRole("Read") ||
                //    context.User.IsInRole("Execute"));
                //});
                //options.AddPolicy("ThreeRoles", policy => policy.Requirements.Add(new ThreeRolesRequirement(new[] { "Admin", "Write", "Read" })));
            });
            //services.AddMvc(options =>
            //{
            //    var policy = new AuthorizationPolicyBuilder()
            //        .RequireAuthenticatedUser()
            //        .RequireRole("Admin", "Write", "Read")
            //        .Build();
            //    options.Filters.Add(new AuthorizeFilter(policy));
            //});
            //services.AddSingleton<IAuthorizationHandler, ThreeRolesHandler>();
            #endregion
            services.AddAutoMapper(typeof(AutoMapperProfile).Assembly);
            #region Add Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "video_editing_api", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme.<br>
                                       Enter 'Bearer' [space] and then your token in the text input below.<br>
                                       Example: 'Bearer 12345abcdef'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference=new OpenApiReference
                                {
                                    Type=ReferenceType.SecurityScheme,
                                    Id="Bearer"
                                },
                                Scheme="oauth2",
                                Name="Bearer",
                                In=ParameterLocation.Header,
                            },
                            new List<string>()
                        }
                    });
            });
            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //if (env.IsDevelopment())
            //{
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "video_editing_api v1"));
            //}

            app.UseCors(_myAllowSpecificOrigins);

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<NotiHub>("/noti");
            });
            //app.UseCors(options =>
            //    options.WithOrigins("https://localhost:5001") // Thay đổi theo domain 
            //    .AllowAnyHeader()
            //    .AllowAnyMethod()
            //    .AllowCredentials()
            //);
        }
    }
}