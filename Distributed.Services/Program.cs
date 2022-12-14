using System.Reflection;
using Application.IoC;
using Application.MainModule.AutoMapper;
using Application.MainModule.Hangfire;
using Application.Security.JsonWebToken;
using FluentValidation.AspNetCore;
using Infrastructure.CrossCutting.JsonConverter;
using Infrastructure.CrossCutting.Logging;
using Infrastructure.CrossCutting.Wrapper;
using Infrastructure.Data.MainModule.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Stimulsoft.Base;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration));

// Add services to the container.
builder.Services.AddDbContext<MainContext>(opts =>
    //opts.UseInMemoryDatabase("jobagapi"));
    opts.UseMySql("server=localhost;database=jobagdb;user=root;password=root;port=3306",
    new MySqlServerVersion(new Version()), b => b.MigrationsAssembly("Distributed.Services")));
    //opts.UseSqlServer("server=bmoiiwtntdi7cbmqd02u-mysql.services.clever-cloud.com;port=3306;database=bmoiiwtntdi7cbmqd02u;uid=usrgzcojkoczyrt2;password=QRd8zHCQlP2U8CSeTkX3", b=>b.MigrationsAssembly("Distributed.Services")));

builder.Services.AddAutoMapper(typeof(AutoMapperConfiguration).GetTypeInfo().Assembly);
builder.Services.Configure<JwtIssuerOptions>(builder.Configuration.GetSection("JwtIssuerOptions"));

builder.Services.AddDependencyInjectionInterfaces();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new TrimStringConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values.SelectMany(x => x.Errors.Select(e => e.ErrorMessage)).ToList();

            return new BadRequestObjectResult(new JsonResult<string> { Message = errors[0] });
        };
    });

builder.Services.AddFluentValidationAutoValidation().AddHangfireConfig().AddHttpContextAccessor();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("JwtIssuerOptions").Get<JwtIssuerOptions>();
        options.ClaimsIssuer = jwtOptions.Issuer;
        options.IncludeErrorDetails = true;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateActor = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = jwtOptions.SymmetricSecurityKey,
            ClockSkew = TimeSpan.Zero
        };

        options.SaveToken = true;

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                // If the request is for our hub...
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/notification"))
                {
                    // Read the token out of the query string
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    var urlList = builder.Configuration.GetSection("AllowedOrigin").GetChildren().ToArray()
        .Select(c => c.Value.TrimEnd('/'))
        .ToArray();
    options.AddPolicy("CorsPolicy",
        p => p.WithOrigins(urlList)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo {Title = "Jobag", Version = "v1"});
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();