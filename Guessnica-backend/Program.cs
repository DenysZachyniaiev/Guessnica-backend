using Guessnica_backend.Data;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<IFacebookAuthService, FacebookAuthService>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
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
            Title  = "Validation failed"
        };
        return new BadRequestObjectResult(problem);
    };
});

builder.Services
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

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddFacebook(options =>
{
    options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
    options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
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

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
});

builder.Services.AddSwaggerGen(c =>
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

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                     ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
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

builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection("EmailOptions"))
    .ValidateDataAnnotations()
    .Validate(opt => opt.User == opt.FromEmail, "For Gmail, User must equal FromEmail")
    .ValidateOnStart();

builder.Services.AddScoped<IAppEmailSender, MailKitEmailSender>();

builder.Services.AddScoped<IJwtService, JwtService>();

var app = builder.Build();

app.UseExceptionHandler("/error");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(app.Environment.IsDevelopment() ? "DevCors" : "ProdCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Map("/error", (HttpContext httpContext) =>
{
    var feature = httpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var ex = feature?.Error;
    return Results.Problem(
        title: "Unexpected error",
        detail: app.Environment.IsDevelopment() ? ex?.ToString() : null,
        statusCode: StatusCodes.Status500InternalServerError
    );
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    foreach (var r in new[] { "User", "Admin" })
    {
        if (!await roleManager.RoleExistsAsync(r))
        {
            await roleManager.CreateAsync(new IdentityRole(r));
        }
    }
    
    var seedEmail = "test@example.com";
    var user = await userManager.FindByEmailAsync(seedEmail);
    if (user == null)
    {
        user = new AppUser
        {
            UserName = seedEmail,
            Email = seedEmail,
            DisplayName = "Test User",
            EmailConfirmed = true
        };
        var create = await userManager.CreateAsync(user, "Haslo123!");
        if (!create.Succeeded)
            throw new Exception(string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}")));
    }

    if (!await userManager.IsInRoleAsync(user, "User"))
    {
        await userManager.AddToRoleAsync(user, "User");
    }
}

app.Run();