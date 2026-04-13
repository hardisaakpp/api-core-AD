using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Helpers;
using SCNexus.Core.API.Models;
using System.Text;
using SCNexus.Core.API.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Configurar conexión a SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 🔹 Configurar Identity
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 🔹 Leer clave JWT de forma segura
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("❌ No se encontró la clave JWT en appsettings.json (Jwt:Key)");
}

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

// 🔹 Configurar autenticación JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
    };
});

// 🔹 Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                  {
                      // Permitir localhost en cualquier puerto (dev)
                      var uri = new Uri(origin);
                      return uri.Host == "localhost" ||
                             uri.Host == "127.0.0.1" ||
                             // Red interna de producción / staging / dev
                             uri.Host.StartsWith("192.168.") ||
                             uri.Host.StartsWith("210.17.1.") ||
                             uri.Host.StartsWith("172.30.");
                  })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AdLdapSyncService>();

// Try to determine HTTPS port from environment/config to avoid the warning:
// "Failed to determine the https port for redirect." This commonly occurs when
// running outside of Visual Studio where launchSettings.json is not used.
var httpsPortSetting = builder.Configuration["ASPNETCORE_HTTPS_PORT"];
if (int.TryParse(httpsPortSetting, out var httpsPort))
{
    // Configure HttpsRedirectionOptions in DI so app.UseHttpsRedirection() knows the port
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options => options.HttpsPort = httpsPort);
}

var app = builder.Build();

// 🔹 Middleware
// Habilitar Swagger en todos los entornos
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seedear datos iniciales (roles, permisos, usuario admin)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        await DataSeeder.SeedAsync(context, userManager, roleManager);
        Console.WriteLine("✅ Datos iniciales cargados correctamente");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error al cargar datos iniciales: {ex.Message}");
    }
}

app.Run();
