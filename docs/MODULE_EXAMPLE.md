# Ejemplo de Módulo Independiente: Depósitos

Este es un ejemplo de cómo crear un módulo completamente desacoplado del Core.

## Estructura del Módulo

```
ModuloDepositos/
├── appsettings.json
├── Program.cs
├── Controllers/
│   └── DepositosController.cs
├── Models/
│   └── Deposito.cs
└── Data/
    └── DepositosDbContext.cs
```

## 1. appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ModuloDepositos;Trusted_Connection=True;"
  },
  "Authentication": {
    "JwtKey": "LA_MISMA_SECRET_KEY_DEL_CORE_DEBE_SER_IDENTICA",
    "JwtIssuer": "https://core-api.example.com",
    "JwtAudience": "https://core-api.example.com"
  }
}
```

## 2. Program.cs

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Conexión a base de datos del módulo (NO usa la base de datos del Core)
builder.Services.AddDbContext<DepositosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar autenticación JWT (valida localmente sin llamar al Core)
var jwtKey = builder.Configuration["Authentication:JwtKey"];
if (string.IsNullOrEmpty(jwtKey))
    throw new InvalidOperationException("JWT Key no configurada");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Authentication:JwtIssuer"],
            ValidAudience = builder.Configuration["Authentication:JwtAudience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // ← Valida JWT automáticamente
app.UseAuthorization();
app.MapControllers();

app.Run();
```

## 3. Models/Deposito.cs

```csharp
public class Deposito
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Aprobado, Rechazado
    
    // NO tiene FK a AppUser, solo guarda el ID
    public string CreadoPorUserId { get; set; } = string.Empty;
    public string CreadoPorUsername { get; set; } = string.Empty;
}
```

## 4. Data/DepositosDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;

public class DepositosDbContext : DbContext
{
    public DepositosDbContext(DbContextOptions<DepositosDbContext> options)
        : base(options)
    {
    }

    public DbSet<Deposito> Depositos { get; set; }

    // NO tiene DbSet<AppUser>, NO tiene DbSet<Permission>
    // Este módulo NO conoce las tablas del Core
}
```

## 5. Controllers/DepositosController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requiere token válido
public class DepositosController : ControllerBase
{
    private readonly DepositosDbContext _context;

    public DepositosController(DepositosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Listar depósitos (requiere permiso "depositos.view")
    /// </summary>
    [HttpGet]
    public IActionResult GetDepositos()
    {
        // Verificar permiso desde el token
        if (!HasPermission("depositos.view"))
            return Forbid();

        var depositos = _context.Depositos.ToList();
        return Ok(depositos);
    }

    /// <summary>
    /// Crear depósito (requiere permiso "depositos.create")
    /// </summary>
    [HttpPost]
    public IActionResult CreateDeposito([FromBody] CreateDepositoRequest request)
    {
        if (!HasPermission("depositos.create"))
            return Forbid();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        var deposito = new Deposito
        {
            Numero = request.Numero,
            Monto = request.Monto,
            Fecha = DateTime.UtcNow,
            Estado = "Pendiente",
            CreadoPorUserId = userId ?? string.Empty,
            CreadoPorUsername = username ?? string.Empty
        };

        _context.Depositos.Add(deposito);
        _context.SaveChanges();

        return Ok(deposito);
    }

    /// <summary>
    /// Aprobar depósito (requiere permiso "depositos.approve")
    /// </summary>
    [HttpPost("{id}/approve")]
    public IActionResult ApproveDeposito(int id)
    {
        if (!HasPermission("depositos.approve"))
            return Forbid();

        var deposito = _context.Depositos.Find(id);
        if (deposito == null)
            return NotFound();

        deposito.Estado = "Aprobado";
        _context.SaveChanges();

        return Ok(deposito);
    }

    /// <summary>
    /// Helper: verifica si el usuario tiene un permiso específico
    /// </summary>
    private bool HasPermission(string permission)
    {
        var permissions = User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        return permissions.Contains(permission);
    }

    /// <summary>
    /// Helper: verifica si el usuario tiene un rol específico
    /// </summary>
    private bool HasRole(string role)
    {
        return User.IsInRole(role);
    }
}

public class CreateDepositoRequest
{
    public string Numero { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}
```

## 6. Middleware Opcional: Logging de Permisos

```csharp
public class PermissionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PermissionLoggingMiddleware> _logger;

    public PermissionLoggingMiddleware(RequestDelegate next, ILogger<PermissionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = context.User.FindFirst(ClaimTypes.Name)?.Value;
            var roles = context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
            var permissions = context.User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();

            _logger.LogInformation(
                "User {Username} (ID: {UserId}) with roles [{Roles}] and permissions [{Permissions}] accessing {Path}",
                username, userId, string.Join(", ", roles), string.Join(", ", permissions), context.Request.Path);
        }

        await _next(context);
    }
}

// Registrar en Program.cs
app.UseMiddleware<PermissionLoggingMiddleware>();
```

## 7. Docker Compose para el Módulo

```yaml
version: '3.8'

services:
  modulo-depositos-api:
    build: .
    ports:
      - "5002:80"
    environment:
      - ConnectionStrings__DefaultConnection=Server=sql-depositos;Database=ModuloDepositos;User=sa;Password=YourPassword123!
      - Authentication__JwtKey=LA_MISMA_SECRET_KEY_DEL_CORE
      - Authentication__JwtIssuer=https://core-api.example.com
      - Authentication__JwtAudience=https://core-api.example.com
    depends_on:
      - sql-depositos

  sql-depositos:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword123!
    ports:
      - "1434:1433"
```

## Flujo Completo

### 1. Usuario hace login en el Core
```bash
POST https://core-api.example.com/api/auth/login
{
  "userName": "contador1",
  "password": "Password123!"
}
```

**Respuesta:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 10800,
  "user": {
    "roles": ["Contabilidad"],
    "permissions": ["depositos.view", "depositos.create"]
  }
}
```

### 2. Frontend guarda el token
```javascript
localStorage.setItem('token', data.token);
```

### 3. Frontend llama al módulo Depósitos
```bash
GET https://depositos-api.example.com/api/depositos
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 4. El módulo valida el token localmente
- ✅ Firma válida (misma secret key)
- ✅ No expirado
- ✅ Tiene permiso "depositos.view"
- ✅ Retorna los datos

**No hay llamadas al Core en tiempo real.**

## Ventajas

✔ El módulo funciona **independientemente** del Core
✔ No necesita acceso a la base de datos del Core
✔ Puede estar en un servidor diferente
✔ Puede escalarse horizontalmente
✔ Si el Core está caído, el módulo sigue validando tokens
✔ Muy fácil de mantener y desplegar

## Agregar Más Permisos

Si necesitas nuevos permisos:

1. **En el Core:** Crear el permiso y asignarlo a roles
   ```bash
   POST https://core-api.example.com/api/permissions
   {
     "key": "depositos.export",
     "description": "Exportar depósitos",
     "module": "depositos"
   }
   ```

2. **En el Módulo:** Solo agregar la verificación
   ```csharp
   [HttpGet("export")]
   public IActionResult ExportDepositos()
   {
       if (!HasPermission("depositos.export"))
           return Forbid();
       
       // Lógica de exportación...
   }
   ```

3. **Listo!** El usuario que tenga ese permiso podrá exportar.

---

**Sin acoplamiento, sin complicaciones.** 🚀
