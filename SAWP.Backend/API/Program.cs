using System.Security.Claims;
using System.Text;
using API.Common;
using API.Middlewares;
using Core.Configuration;
using Core.Interfaces;
using Infrastructure.Common;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Services.Implementations;
using Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Per-developer overrides (e.g. local SQL Server connection string). Loaded after appsettings.json and
// appsettings.{Environment}.json, so its keys win. The file is git-ignored and optional: copy
// appsettings.Local.example.json to appsettings.Local.json. See "Local Configuration" in README.md.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1. Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Data Access & Repositories (Infrastructure -> Core Interfaces)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<ILunchRepository, LunchRepository>();
builder.Services.AddScoped<IClassScheduleRepository, ClassScheduleRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.Configure<SchoolOptions>(builder.Configuration.GetSection(SchoolOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));

builder.Services.AddSingleton<ISchoolClock, SchoolClock>();

// 3. Business Logic Services (Services -> Services Interfaces)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountLinkService, AccountLinkService>();
builder.Services.AddScoped<IAccountRequestService, AccountRequestService>();
builder.Services.AddScoped<ISessionValidator, SessionValidator>();
builder.Services.AddSingleton<IBackgroundWorkQueue, BackgroundWorkQueue>();
builder.Services.AddHostedService<BackgroundWorkService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<AccountLinkOptions>(builder.Configuration.GetSection(AccountLinkOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestOriginSource, HttpRequestOriginSource>();

// Emailed activation / recovery links point to the web client. Its addresses must be listed
// (AccountLinks:FrontendOrigins, e.g. environment variables AccountLinks__FrontendOrigins__0, __1, ...);
// a link uses the address the user is on only if it is in this list.
var frontendOrigins = builder.Configuration.GetSection($"{AccountLinkOptions.SectionName}:FrontendOrigins").Get<List<string>>() ?? new();
if (frontendOrigins.Count == 0 || frontendOrigins.Any(o => AccountLinkService.NormalizeOrigin(o) == null))
{
    throw new InvalidOperationException(
        "AccountLinks:FrontendOrigins must list the web client addresses as http(s)://host[:port], e.g. https://sawp.example.edu.vn.");
}
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IGuardianService, GuardianService>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILunchService, LunchService>();
builder.Services.AddScoped<IRequestWorkflowEngine, RequestWorkflowEngine>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IStudentImporter, StudentImporter>();
builder.Services.AddScoped<IStaffImporter, StaffImporter>();
builder.Services.AddScoped<IPlacementPlanService, PlacementPlanService>();
builder.Services.AddScoped<IEContactBookService, EContactBookService>();

// 4. External Services (Infrastructure ExternalServices -> Interfaces)
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IFileStorageService, FileStorageService>();

// 5. Authentication & Authorization (JWT Bearer)
// Jwt:Key signs every login token, so it must be a secret supplied per environment:
// appsettings.Development.json for local development, an environment variable (Jwt__Key) or a
// secret store elsewhere. There is no fallback: a missing or short key stops the app.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be set to a secret of at least 32 bytes (e.g. environment variable Jwt__Key).");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SAWP.Backend";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SAWP.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // BR-03, BR-48: a token is accepted only while the account is Active and its session version is current,
    // so deactivation and password changes end existing sessions at once.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            if (!int.TryParse(principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                context.Fail("Missing user id.");
                return;
            }
            // Tokens issued before session versions existed carry none; they match version 0 until the first change
            _ = int.TryParse(principal!.FindFirst(AuthService.SessionVersionClaim)?.Value, out var sessionVersion);

            var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
            var state = await validator.CheckAsync(userId, sessionVersion, context.HttpContext.RequestAborted);
            if (!state.IsValid)
            {
                context.Fail("The session has ended.");
                return;
            }
            context.HttpContext.Items[PasswordChangeGate.ItemKey] = state.MustChangePassword;
        }
    };
});

builder.Services.AddAuthorization();

// 6. API Controllers & Swagger with JWT support
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SAWP API", Version = "v1" });

    // Bearer token support in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders(PasswordChangeGate.HeaderName); // read by the web client (UC-60 A1)
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable Swagger documentation & UI for local testing and development
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SAWP API v1");
    c.RoutePrefix = "swagger";
});

// Redirect root to swagger UI
app.MapGet("/", () => Results.Redirect("/swagger"));

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowAll");

// Auto apply pending migrations and seed reference data.
// Demo data (accounts with a shared default password, sample students) is seeded only in
// Development, or when Seed:DemoData is true (e.g. a demo server); never enable it in production.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await DbInitializer.SeedRolesAsync(dbContext);

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:DemoData"))
    {
        await DbInitializer.SeedDemoDataAsync(dbContext);
    }

    var firstAdmin = app.Configuration.GetSection(FirstAdminOptions.SectionName).Get<FirstAdminOptions>();
    var firstAdminSection = FirstAdminOptions.SectionName;
    switch (await DbInitializer.SeedFirstAdminAsync(dbContext, firstAdmin))
    {
        case FirstAdminResult.Created:
            app.Logger.LogWarning(
                "Created the first admin account '{Username}' from {Section}. Remove {Section}:Password from the configuration now.",
                firstAdmin!.Username, firstAdminSection, firstAdminSection);
            break;
        case FirstAdminResult.AdminExists when !string.IsNullOrEmpty(firstAdmin?.Password):
            app.Logger.LogWarning(
                "An admin account already exists, so {Section} is ignored. Remove {Section}:Password from the configuration.",
                firstAdminSection, firstAdminSection);
            break;
        case FirstAdminResult.NotConfigured:
            app.Logger.LogWarning(
                "No admin account exists. Set {Section}:Username, {Section}:Email and {Section}:Password " +
                "(e.g. environment variables Seed__Admin__Username, Seed__Admin__Email, Seed__Admin__Password) to create one.",
                firstAdminSection, firstAdminSection, firstAdminSection);
            break;
    }
}

// Authentication must precede Authorization
app.UseAuthentication();
app.UseMiddleware<PasswordChangeGate>();
app.UseAuthorization();

app.MapControllers();


app.Run();
