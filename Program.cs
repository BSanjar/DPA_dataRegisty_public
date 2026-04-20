using data_registry_public.Integrations;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


//????????? ??????????? DpaDb2Context ? ??????????
builder.Services.AddDbContext<AppDbContext>(
    options =>
    {
        options
        .UseLoggerFactory(LoggerFactory.Create(builder => { }))
        /*.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))*/
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => //CookieAuthenticationOptions
    {
        options.LoginPath = new Microsoft.AspNetCore.Http.PathString("/esi/Login");
    });

//??????????? 
Log.Logger = new LoggerConfiguration()
    .WriteTo.File($"logs/{DateTime.Now.ToString("dd-MM-yyyy")}.log", rollingInterval: RollingInterval.Day) // ??????????? ? ????
     .CreateLogger();

//builder.Services.AddLogging(b => b.AddSerilog());


builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

builder.Services.AddHttpClient();

// ?????????? ? ???????? (?????? ? ????????). ????? ??????????? ????????? API
// ?????????? ???????? MinJustService ?? ???????? ?????????? IMinJustService.
builder.Services.AddScoped<IMinJustService, MinJustService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
