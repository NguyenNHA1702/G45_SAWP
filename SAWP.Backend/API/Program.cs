using API.Middlewares;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Services.Implementations;
using Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Data Access & Repositories (Infrastructure -> Core Interfaces)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<ILunchRepository, LunchRepository>();

// 3. Business Logic Services (Services -> Services Interfaces)
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILunchService, LunchService>();
builder.Services.AddScoped<IRequestWorkflowEngine, RequestWorkflowEngine>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStudentImporter, StudentImporter>();
builder.Services.AddScoped<IStaffImporter, StaffImporter>();

// 4. External Services (Infrastructure ExternalServices -> Interfaces)
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IFileStorageService, FileStorageService>();

// 5. API Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseMiddleware<JwtMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
