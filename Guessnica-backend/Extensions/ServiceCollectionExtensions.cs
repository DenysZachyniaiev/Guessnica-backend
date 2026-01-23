using Guessnica_backend.Data;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Guessnica_backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGuessnicaControllers(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        return services;
    }

    public static IServiceCollection AddGuessnicaDb(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        return services;
    }

    public static IServiceCollection AddGuessnicaApiBehavior(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                var problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed"
                };
                return new BadRequestObjectResult(problem);
            };
        });

        return services;
    }

    public static IServiceCollection AddGuessnicaIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddGuessnicaAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient<IFacebookAuthService, FacebookAuthService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddFacebook(options =>
        {
            options.AppId = config["Authentication:Facebook:AppId"]!;
            options.AppSecret = config["Authentication:Facebook:AppSecret"]!;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(config["Jwt:Key"]!)
                ),

                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                    var sub = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                              ?? ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (string.IsNullOrEmpty(sub))
                    {
                        ctx.Fail("missing_sub");
                        return;
                    }

                    var user = await userManager.FindByIdAsync(sub);
                    if (user is null)
                    {
                        ctx.Fail("user_not_found");
                        return;
                    }

                    var tokenStamp = ctx.Principal?.FindFirst("sstamp")?.Value;
                    var currentStamp = await userManager.GetSecurityStampAsync(user);
                    if (string.IsNullOrEmpty(tokenStamp) || tokenStamp != currentStamp)
                    {
                        ctx.Fail("token_revoked");
                    }
                },

                OnChallenge = ctx =>
                {
                    if (!ctx.Response.HasStarted)
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";

                        var code = ctx.AuthenticateFailure?.Message ?? ctx.Error ?? "unauthorized";
                        ctx.Response.Headers["WWW-Authenticate"] = "Bearer error=\"invalid_token\"";

                        return ctx.Response.WriteAsync($$"""{"code":"{{code}}","message":"Missing or invalid token"}""");
                    }
                    return Task.CompletedTask;
                },

                OnAuthenticationFailed = ctx =>
                {
                    Console.WriteLine($"JWT failed: {ctx.Exception.Message}");
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
            options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
        });

        return services;
    }

    public static IServiceCollection AddGuessnicaSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Guessnica-backend", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Wklej tylko token (bez słowa 'Bearer')."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddGuessnicaCors(this IServiceCollection services, IConfiguration config)
    {
        var allowedOrigins = config.GetSection("AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:5173" };

        services.AddCors(options =>
        {
            options.AddPolicy("DevCors", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });

            options.AddPolicy("ProdCors", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection AddGuessnicaAppServices(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(config.GetSection("EmailOptions"))
            .ValidateDataAnnotations()
            .Validate(opt => opt.User == opt.FromEmail, "For Gmail, User must equal FromEmail")
            .ValidateOnStart();

        services.AddScoped<IAppEmailSender, MailKitEmailSender>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IRiddleService, RiddleService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
    
    public static IServiceCollection AddGuessnicaHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }
}