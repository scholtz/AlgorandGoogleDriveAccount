
using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Drive.v3;
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
            builder.Services.AddSingleton<GoogleDriveRepository>();

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
                    options.Scope.Add(DriveService.Scope.DriveFile);
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                });

            builder.Services.AddControllersWithViews();
            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
