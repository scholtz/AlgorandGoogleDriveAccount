using AlgorandGoogleDriveAccount.MCP;
using AlgorandGoogleDriveAccount.Model;
using AlgorandGoogleDriveAccount.Repository;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
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
            builder.Services.Configure<CorsConfiguration>(builder.Configuration.GetSection("Cors"));
            builder.Services.Configure<CrossAccountProtectionConfiguration>(builder.Configuration.GetSection("CrossAccountProtection"));

            // Add CORS configuration
            var corsConfig = new CorsConfiguration();
            builder.Configuration.GetSection("Cors").Bind(corsConfig);
            
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policyBuilder =>
                {
                    var origins = corsConfig.AllowedOrigins?.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray() ?? Array.Empty<string>();
                    
                    if (origins.Length > 0)
                    {
                        policyBuilder.WithOrigins(origins);
                    }
                    else
                    {
                        // If no origins are configured, allow any origin in development
                        if (builder.Environment.IsDevelopment())
                        {
                            policyBuilder.AllowAnyOrigin();
                        }
                        else
                        {
                            // In production, don't allow any origin if none configured
                            policyBuilder.WithOrigins();
                        }
                    }
                    
                    policyBuilder.AllowAnyMethod()
                               .AllowAnyHeader();
                    
                    // Only allow credentials if we have specific origins (not AllowAnyOrigin)
                    if (origins.Length > 0)
                    {
                        policyBuilder.AllowCredentials();
                    }
                });

                // Add a named policy for API endpoints that might need different CORS settings
                options.AddPolicy("ApiPolicy", policyBuilder =>
                {
                    var origins = corsConfig.AllowedOrigins?.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray() ?? Array.Empty<string>();
                    
                    if (origins.Length > 0)
                    {
                        policyBuilder.WithOrigins(origins);
                    }
                    else if (builder.Environment.IsDevelopment())
                    {
                        policyBuilder.AllowAnyOrigin();
                    }
                    
                    policyBuilder.AllowAnyMethod()
                               .AllowAnyHeader();
                    
                    if (origins.Length > 0)
                    {
                        policyBuilder.AllowCredentials();
                    }
                });
            });

            builder.Services.AddSingleton<GoogleDriveRepository>();

            // Add business logic services
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.IDevicePairingService, AlgorandGoogleDriveAccount.BusinessLogic.DevicePairingService>();
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.IDriveService, AlgorandGoogleDriveAccount.BusinessLogic.DriveService>();
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.IGoogleAuthorizationService, AlgorandGoogleDriveAccount.BusinessLogic.GoogleAuthorizationService>();
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.ICrossAccountProtectionService, AlgorandGoogleDriveAccount.BusinessLogic.CrossAccountProtectionService>();
            builder.Services.AddScoped<AlgorandGoogleDriveAccount.BusinessLogic.IPortfolioValuationService, AlgorandGoogleDriveAccount.BusinessLogic.PortfolioValuationService>();
            
            // Add HTTP context accessor for authorization service
            builder.Services.AddHttpContextAccessor();
            
            // Add HttpClient for Cross-Account Protection API calls
            builder.Services.AddHttpClient<AlgorandGoogleDriveAccount.BusinessLogic.CrossAccountProtectionService>();

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
                    
                    // Basic scopes - only request what's needed initially
                    options.Scope.Clear(); // Clear default scopes
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    
                    // Note: Cross-Account Protection doesn't require a special scope
                    // It works with standard OAuth scopes and proper security practices
                    
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                    options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
                    
                    // Configure OpenIdConnect protocol validator to handle nonce validation
                    options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            // Support incremental authorization
                            if (context.Properties.Items.ContainsKey("incremental_scopes"))
                            {
                                var additionalScopes = context.Properties.Items["incremental_scopes"];
                                if (!string.IsNullOrEmpty(additionalScopes))
                                {
                                    context.ProtocolMessage.Scope += " " + additionalScopes;
                                    context.ProtocolMessage.SetParameter("include_granted_scopes", "true");
                                }
                            }
                            
                            // Store session information in authentication properties
                            if (context.Properties.Items.ContainsKey("sessionId"))
                            {
                                context.ProtocolMessage.State = context.Properties.Items["sessionId"];
                            }
                            
                            // Configure for incremental authorization and Cross-Account Protection
                            context.ProtocolMessage.SetParameter("include_granted_scopes", "true");
                            context.ProtocolMessage.SetParameter("access_type", "offline");
                            
                            // Enable granular consent for better security
                            context.ProtocolMessage.SetParameter("enable_granular_consent", "true");
                            
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            // Handle successful token validation and Cross-Account Protection
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            // Log the authentication failure for debugging
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogError(context.Exception, "OpenIdConnect authentication failed: {ErrorMessage}", context.Exception?.Message);
                            
                            // Handle nonce validation errors specifically
                            if (context.Exception?.Message?.Contains("nonce") == true)
                            {
                                logger.LogWarning("Nonce validation failed - this may be due to device pairing flow. Continuing with authentication.");
                                context.HandleResponse();
                                // Redirect to an error page or handle gracefully
                                context.Response.Redirect("/pair.html?error=nonce_validation_failed");
                                return Task.CompletedTask;
                            }
                            
                            return Task.CompletedTask;
                        }
                    };
                    
                    // Configure protocol validator to be more lenient with nonce validation for device flows
                    options.ProtocolValidator.RequireNonce = false;
                });

            builder.Services.AddControllersWithViews();

            // Configure MCP Server
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly();


            var app = builder.Build();

            // Log CORS configuration for debugging
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            var corsConfigForLogging = new CorsConfiguration();
            app.Configuration.GetSection("Cors").Bind(corsConfigForLogging);
            
            if (corsConfigForLogging.AllowedOrigins?.Any() == true)
            {
                logger.LogInformation("CORS configured with allowed origins: {AllowedOrigins}", 
                    string.Join(", ", corsConfigForLogging.AllowedOrigins));
            }
            else
            {
                logger.LogWarning("No CORS origins configured. Using default policy based on environment.");
            }

            app.UseSwagger();
            app.UseSwaggerUI();

            // Enable static files with proper content types
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    // Ensure HTML files are served with UTF-8 charset
                    if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Context.Response.Headers.Append("Content-Type", "text/html; charset=utf-8");
                    }
                }
            });

            // Enable CORS
            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapMcp("/mcp");

            // Map default route to index.html
            app.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
            });

            _ = app.Services.GetService<GoogleDriveRepository>();
            _ = app.Services.GetService<BiatecMCPGoogle>();

            app.Run();
        }
    }
}
