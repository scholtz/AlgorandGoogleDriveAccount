using AlgorandGoogleDriveAccount.MCP;
using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace AlgorandGoogleDriveAccount
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddProblemDetails();

            var config = new Configuration();
            builder.Configuration.GetSection("App").Bind(config);
            builder.Services.Configure<Model.Configuration>(builder.Configuration.GetSection("App"));
            builder.Services.Configure<AesOptions>(builder.Configuration.GetSection("AesOptions"));
            builder.Services.Configure<RedisConfiguration>(builder.Configuration.GetSection("Redis"));
            builder.Services.AddSingleton<GoogleDriveRepository>();

            // Add business logic services
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.IDevicePairingService, AlgorandGoogleDriveAccount.BusinessLogic.DevicePairingService>();
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.IDriveService, AlgorandGoogleDriveAccount.BusinessLogic.DriveService>();

            // Add Redis distributed cache
            var redisConfig = new RedisConfiguration();
            builder.Configuration.GetSection("Redis").Bind(redisConfig);
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConfig.ConnectionString;
            });

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddGoogleOpenIdConnect(options =>
                {
                    options.ClientId = config.ClientId;
                    options.ClientSecret = config.ClientSecret;
                    options.Scope.Add("email");
                    options.Scope.Add(Google.Apis.Drive.v3.DriveService.Scope.DriveFile);
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                });

            builder.Services.AddControllersWithViews();

            // Configure MCP Server
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithTools<BiatecMCPGoogle>();

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            // Enable static files
            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapMcp("/mcp");

            app.Run();
        }
    }
}
