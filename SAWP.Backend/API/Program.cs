using System.Text;
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

builder.Services.AddSingleton<ISchoolClock, SchoolClock>();

// 3. Business Logic Services (Services -> Services Interfaces)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IGuardianService, GuardianService>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILunchService, LunchService>();
builder.Services.AddScoped<IRequestWorkflowEngine, RequestWorkflowEngine>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStudentImporter, StudentImporter>();
builder.Services.AddScoped<IStaffImporter, StaffImporter>();

// 4. External Services (Infrastructure ExternalServices -> Interfaces)
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IFileStorageService, FileStorageService>();

// 5. Authentication & Authorization (JWT Bearer)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SAWP_SUPER_SECRET_SECURITY_KEY_2026_PRIMARY_SCHOOL_PLATFORM!";
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
});

builder.Services.AddAuthorization();

// 6. API Controllers & Swagger with JWT support
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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
              .AllowAnyMethod();
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

// Auto seed demo data if needed
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.SeedAsync(dbContext);
}

// Authentication must precede Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


app.Run();
