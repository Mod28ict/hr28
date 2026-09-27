using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Services;
using HR28.Infrastructure.Data.Seed;



var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddDbContext<HR28DbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVoterService, VoterService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();



builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<HR28DbContext>();

    await RoleSeeder.SeedRolesAsync(dbContext);
    await ConstituencySeeder.SeedAsync(dbContext);
    await IslandSeeder.SeedAsync(dbContext);
}

app.Run();
