# 📘 Guía para Desarrolladores de Módulos - SCNexus Core

## 🎯 Introducción

SCNexus Core es un sistema de autenticación centralizado que gestiona usuarios, roles y permisos para todos los módulos del ecosistema SCNexus. Los módulos externos son aplicaciones .NET independientes que consumen el JWT (token) generado por el Core para validar accesos.

---

## 🏗️ Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────┐
│                      SCNexus CORE API                        │
│  - Autenticación (Local + Active Directory)                 │
│  - Gestión de Usuarios                                       │
│  - Gestión de Roles y Permisos                              │
│  - Generación de JWT con roles y permisos                   │
└─────────────────┬───────────────────────────────────────────┘
                  │ JWT Token
                  │ (incluye roles y permisos)
                  │
    ┌─────────────┼─────────────┬──────────────┬──────────────┐
    │             │             │              │              │
    ▼             ▼             ▼              ▼              ▼
┌────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│Módulo  │  │Módulo    │  │Módulo    │  │Módulo    │  │Módulo    │
│Depósit.│  │Contabil. │  │Inventar. │  │Facturac. │  │Reportes  │
└────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘
     ↑            ↑             ↑             ↑             ↑
     └────────────┴─────────────┴─────────────┴─────────────┘
              Lee y valida JWT (NO llama al Core)
```

---

## 🔑 Conceptos Clave

### **PERMISO**
- Es una **acción específica** que un usuario puede realizar
- Formato: `modulo.accion` (ej: `inventarios.view`, `depositos.create`)
- Se crean en el **CORE** mediante API
- Se incluyen en el **JWT** cuando el usuario hace login

### **ROL**
- Es un **grupo de permisos** asignado a usuarios
- Ejemplos: `Admin`, `Gerente`, `Inventarista`, `Contador`
- Un usuario puede tener múltiples roles
- Se crean en el **CORE** mediante código o API

### **JWT (Token)**
- Token firmado que contiene:
  - ID del usuario
  - Nombre de usuario
  - Email
  - **Lista de roles** (claims: `role`)
  - **Lista de permisos** (claims: `permission`)
- Expira en 3 horas por defecto
- Los módulos lo validan SIN llamar al Core

---

## 📋 Ejemplo: Módulo de Inventarios

### **1. Definir las Acciones del Módulo**

Primero identifica qué puede hacer un usuario en tu módulo:

| Acción | Descripción |
|--------|-------------|
| Ver inventarios | Listar inventarios existentes |
| Crear inventario | Iniciar un nuevo inventario |
| Realizar inventario | Ejecutar conteo físico |
| Aprobar inventario | Cerrar y aprobar diferencias |
| Exportar inventario | Generar reporte Excel |

### **2. Crear Permisos en el CORE**

Cada acción = 1 permiso. Los permisos se crean en el Core:

```bash
# Endpoint: POST http://core-api:8080/api/Permissions
# Headers: Authorization: Bearer {TOKEN_ADMIN}

# Permiso 1: Ver inventarios
{
  "key": "inventarios.view",
  "description": "Ver inventarios",
  "module": "inventarios"
}

# Permiso 2: Crear inventario
{
  "key": "inventarios.create",
  "description": "Crear inventario",
  "module": "inventarios"
}

# Permiso 3: Realizar inventario
{
  "key": "inventarios.realizar",
  "description": "Realizar conteo de inventario",
  "module": "inventarios"
}

# Permiso 4: Aprobar inventario
{
  "key": "inventarios.aprobar",
  "description": "Aprobar y cerrar inventarios",
  "module": "inventarios"
}

# Permiso 5: Exportar inventario
{
  "key": "inventarios.export",
  "description": "Exportar inventarios a Excel",
  "module": "inventarios"
}
```

**Script PowerShell para crear permisos:**

```powershell
$token = "TU_TOKEN_DE_ADMIN"
$coreUrl = "http://192.168.2.12:8080"

$permisos = @(
    @{key="inventarios.view"; description="Ver inventarios"; module="inventarios"},
    @{key="inventarios.create"; description="Crear inventario"; module="inventarios"},
    @{key="inventarios.realizar"; description="Realizar conteo"; module="inventarios"},
    @{key="inventarios.aprobar"; description="Aprobar inventarios"; module="inventarios"},
    @{key="inventarios.export"; description="Exportar inventarios"; module="inventarios"}
)

foreach ($permiso in $permisos) {
    Invoke-RestMethod -Uri "$coreUrl/api/Permissions" `
        -Method Post `
        -Headers @{Authorization="Bearer $token"} `
        -ContentType "application/json" `
        -Body ($permiso | ConvertTo-Json)
}
```

### **3. Asignar Permisos a Roles**

Decide qué roles tendrán acceso a cada permiso:

| Rol | Permisos Asignados |
|-----|-------------------|
| **Admin** | Todos (automático) |
| **Inventarista** | `inventarios.view`, `inventarios.create`, `inventarios.realizar` |
| **Gerente** | `inventarios.view`, `inventarios.aprobar`, `inventarios.export` |
| **User** | `inventarios.view` |

```bash
# Obtener ID de permisos
GET http://core-api:8080/api/Permissions

# Asignar permisos al rol "Inventarista"
POST http://core-api:8080/api/Permissions/assign
{
  "roleName": "Inventarista",
  "permissionId": 21  # ID del permiso inventarios.view
}

POST http://core-api:8080/api/Permissions/assign
{
  "roleName": "Inventarista",
  "permissionId": 22  # ID del permiso inventarios.create
}

# ... repetir para cada permiso
```

### **4. Crear el Proyecto del Módulo**

```bash
# Crear proyecto Web API
dotnet new webapi -n SCNexus.Inventarios
cd SCNexus.Inventarios

# Instalar paquetes necesarios
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

### **5. Configurar JWT en el Módulo**

**appsettings.json:**

```json
{
  "Jwt": {
    "Key": "MISMA_CLAVE_DEL_CORE_DEBE_SER_IDENTICA",
    "Issuer": "SCNexus.Core",
    "Audience": "SCNexus.Users"
  },
  "CoreApi": {
    "BaseUrl": "http://192.168.2.12:8080"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=210.17.1.54;Database=SCNexus_Inventarios;User Id=sa;Password=tu_password;"
  }
}
```

**Program.cs:**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar autenticación JWT (MISMO del CORE)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();  // ⚠️ IMPORTANTE: Antes de UseAuthorization
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### **6. Implementar Controllers con Validación**

**InventariosController.cs:**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SCNexus.Inventarios.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere JWT válido
    public class InventariosController : ControllerBase
    {
        private readonly IInventarioService _service;
        private readonly ILogger<InventariosController> _logger;

        public InventariosController(IInventarioService service, ILogger<InventariosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// GET: api/Inventarios
        /// Listar todos los inventarios
        /// Requiere permiso: inventarios.view
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetInventarios()
        {
            // Validar permiso desde el JWT
            if (!HasPermission("inventarios.view"))
            {
                _logger.LogWarning("Usuario {User} intentó acceder sin permiso inventarios.view", 
                    User.Identity?.Name);
                return Forbid(); // HTTP 403
            }

            var inventarios = await _service.GetAllAsync();
            return Ok(inventarios);
        }

        /// <summary>
        /// POST: api/Inventarios
        /// Crear un nuevo inventario
        /// Requiere permiso: inventarios.create
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateInventario([FromBody] CreateInventarioRequest request)
        {
            if (!HasPermission("inventarios.create"))
                return Forbid();

            var inventario = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetInventario), new { id = inventario.Id }, inventario);
        }

        /// <summary>
        /// GET: api/Inventarios/{id}
        /// Obtener un inventario específico
        /// Requiere permiso: inventarios.view
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetInventario(int id)
        {
            if (!HasPermission("inventarios.view"))
                return Forbid();

            var inventario = await _service.GetByIdAsync(id);
            if (inventario == null) return NotFound();
            
            return Ok(inventario);
        }

        /// <summary>
        /// PUT: api/Inventarios/{id}/realizar
        /// Realizar conteo físico de inventario
        /// Requiere permiso: inventarios.realizar
        /// </summary>
        [HttpPut("{id}/realizar")]
        public async Task<IActionResult> RealizarInventario(int id, [FromBody] RealizarInventarioRequest request)
        {
            if (!HasPermission("inventarios.realizar"))
                return Forbid();

            await _service.RealizarConteoAsync(id, request);
            return Ok(new { message = "Conteo realizado exitosamente" });
        }

        /// <summary>
        /// PUT: api/Inventarios/{id}/aprobar
        /// Aprobar y cerrar inventario
        /// Requiere permiso: inventarios.aprobar
        /// </summary>
        [HttpPut("{id}/aprobar")]
        public async Task<IActionResult> AprobarInventario(int id)
        {
            if (!HasPermission("inventarios.aprobar"))
                return Forbid();

            await _service.AprobarAsync(id, GetUserId());
            return Ok(new { message = "Inventario aprobado exitosamente" });
        }

        /// <summary>
        /// GET: api/Inventarios/{id}/export
        /// Exportar inventario a Excel
        /// Requiere permiso: inventarios.export
        /// </summary>
        [HttpGet("{id}/export")]
        public async Task<IActionResult> ExportarInventario(int id)
        {
            if (!HasPermission("inventarios.export"))
                return Forbid();

            var fileBytes = await _service.ExportToExcelAsync(id);
            return File(fileBytes, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"inventario_{id}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // ========== MÉTODOS AUXILIARES ==========

        /// <summary>
        /// Verifica si el usuario actual tiene un permiso específico
        /// </summary>
        private bool HasPermission(string permission)
        {
            return User.HasClaim("permission", permission);
        }

        /// <summary>
        /// Obtiene el ID del usuario desde el JWT
        /// </summary>
        private string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        /// <summary>
        /// Obtiene el nombre del usuario desde el JWT
        /// </summary>
        private string GetUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        }

        /// <summary>
        /// Verifica si el usuario tiene un rol específico
        /// </summary>
        private bool HasRole(string role)
        {
            return User.IsInRole(role);
        }
    }
}
```

---

## 🔐 Validación de Permisos - Mejores Prácticas

### **Opción 1: Validación Manual (Recomendada)**

```csharp
[HttpGet]
public async Task<IActionResult> GetData()
{
    if (!User.HasClaim("permission", "modulo.view"))
        return Forbid();
    
    // Tu lógica aquí
}
```

✅ **Ventajas:**
- Control total sobre la validación
- Puedes hacer validaciones complejas
- Puedes registrar auditoría antes de denegar

### **Opción 2: Custom Attribute (Avanzado)**

Crea un atributo personalizado para validar permisos:

```csharp
// Attributes/RequirePermissionAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permission) : base(typeof(PermissionFilter))
    {
        Arguments = new object[] { permission };
    }
}

public class PermissionFilter : IAuthorizationFilter
{
    private readonly string _permission;

    public PermissionFilter(string permission)
    {
        _permission = permission;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!user.HasClaim("permission", _permission))
        {
            context.Result = new ForbidResult();
        }
    }
}
```

**Uso:**

```csharp
[HttpGet]
[RequirePermission("inventarios.view")]
public async Task<IActionResult> GetInventarios()
{
    // Si llega aquí, el usuario tiene el permiso
    return Ok(await _service.GetAllAsync());
}
```

---

## 🧪 Testing del Módulo

### **1. Obtener Token del Core**

```bash
# Login con usuario admin
curl -X POST http://192.168.2.12:8080/api/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{
    "userName": "admin",
    "password": "Admin123!"
  }'

# Response incluye el token JWT
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 10800,
  "user": {
    "id": "abc123",
    "userName": "admin",
    "roles": ["Admin"],
    "permissions": ["inventarios.view", "inventarios.create", "inventarios.realizar", ...]
  }
}
```

### **2. Probar Endpoints del Módulo**

```bash
# Usar el token para acceder al módulo
curl -X GET http://localhost:5001/api/Inventarios \
  -H 'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'

# Si el usuario tiene el permiso "inventarios.view", obtendrá los datos
# Si no tiene el permiso, recibirá HTTP 403 Forbidden
```

### **3. Decodificar JWT para Debugging**

Visita https://jwt.io y pega tu token para ver los claims:

```json
{
  "nameid": "abc123",
  "unique_name": "admin",
  "email": "admin@scnexus.local",
  "role": "Admin",
  "permission": [
    "users.view",
    "users.create",
    "inventarios.view",
    "inventarios.create",
    "inventarios.realizar"
  ],
  "exp": 1737954000,
  "iss": "SCNexus.Core",
  "aud": "SCNexus.Users"
}
```

---

## 📊 Flujo Completo de Autenticación

```
┌──────────┐         ┌───────────────┐         ┌─────────────┐
│ Usuario  │         │  CORE API     │         │  Módulo     │
│          │         │               │         │ Inventarios │
└────┬─────┘         └───────┬───────┘         └──────┬──────┘
     │                       │                        │
     │ 1. POST /Auth/login   │                        │
     │─────────────────────>│                        │
     │   user/pass           │                        │
     │                       │                        │
     │ 2. JWT + roles/perms  │                        │
     │<─────────────────────│                        │
     │                       │                        │
     │ 3. GET /Inventarios   │                        │
     │    Header: Bearer JWT │                        │
     │────────────────────────────────────────────>│
     │                       │                        │
     │                       │    4. Valida JWT       │
     │                       │    5. Lee claims       │
     │                       │    6. Verifica permiso │
     │                       │                        │
     │ 7. Datos o 403        │                        │
     │<────────────────────────────────────────────│
     │                       │                        │
```

**Notas importantes:**
- El módulo **NUNCA** llama al Core para validar permisos
- El JWT es autosuficiente (contiene toda la info necesaria)
- Si el JWT expira, el usuario debe hacer login nuevamente

---

## 🚨 Errores Comunes

### **❌ Error: Unauthorized (401)**

**Causa:** Token inválido, expirado o no enviado

**Solución:**
```csharp
// Verificar que el header esté correcto
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### **❌ Error: Forbidden (403)**

**Causa:** Usuario autenticado pero sin el permiso necesario

**Solución:**
1. Verificar que el permiso existe en el Core
2. Verificar que el rol del usuario tiene el permiso asignado
3. Regenerar token (hacer login de nuevo)

### **❌ Error: JWT Key no coincide**

**Causa:** `Jwt:Key` diferente entre Core y Módulo

**Solución:**
```json
// DEBE SER IDÉNTICO en ambos appsettings.json
{
  "Jwt": {
    "Key": "EXACTAMENTE_LA_MISMA_CLAVE_EN_TODOS_LOS_PROYECTOS"
  }
}
```

### **❌ Error: Claim "permission" no encontrado**

**Causa:** Token generado antes de que existan los permisos

**Solución:**
1. Crear los permisos en el Core
2. Asignarlos a los roles
3. Hacer login nuevamente para obtener token actualizado

---

## 📚 APIs del CORE - Referencia Rápida

### **Autenticación**

```bash
# Login local
POST /api/Auth/login
{
  "userName": "admin",
  "password": "Admin123!",
  "useActiveDirectory": false
}

# Login Active Directory
POST /api/Auth/login
{
  "userName": "sistemas",
  "password": "***REMOVED***",
  "useActiveDirectory": true
}

# Información del usuario actual
GET /api/Auth/me
Headers: Authorization: Bearer {token}

# Validar token
POST /api/Auth/validate
{
  "token": "eyJhbG..."
}
```

### **Gestión de Permisos**

```bash
# Listar todos los permisos
GET /api/Permissions

# Crear permiso
POST /api/Permissions
{
  "key": "modulo.accion",
  "description": "Descripción del permiso",
  "module": "modulo"
}

# Ver permisos de un rol
GET /api/Permissions/role/{roleName}

# Asignar permiso a rol
POST /api/Permissions/assign
{
  "roleName": "NombreDelRol",
  "permissionId": 123
}

# Quitar permiso de rol
DELETE /api/Permissions/unassign
{
  "roleName": "NombreDelRol",
  "permissionId": 123
}
```

### **Gestión de Usuarios**

```bash
# Listar usuarios
GET /api/Users

# Crear usuario local
POST /api/Users?password=Pass123!&role=User
{
  "userName": "jperez",
  "email": "jperez@empresa.com",
  "fullName": "Juan Pérez"
}

# Actualizar usuario
PUT /api/Users/{userId}
{
  "fullName": "Juan Pérez Actualizado",
  "isActive": true
}

# Asignar rol a usuario
POST /api/Users/{userId}/roles
{
  "role": "Gerente"
}

# Quitar rol de usuario
DELETE /api/Users/{userId}/roles/{roleName}

# Sincronizar usuarios desde AD
POST /api/Admin/sync-ldap
```

---

## 🎯 Checklist para Crear un Nuevo Módulo

- [ ] Definir las acciones/páginas del módulo
- [ ] Crear permisos en el Core (formato: `modulo.accion`)
- [ ] Decidir qué roles tendrán cada permiso
- [ ] Asignar permisos a roles en el Core
- [ ] Crear proyecto Web API con `dotnet new webapi`
- [ ] Instalar `Microsoft.AspNetCore.Authentication.JwtBearer`
- [ ] Copiar configuración JWT del Core a `appsettings.json`
- [ ] Configurar autenticación en `Program.cs`
- [ ] Implementar controllers con validación de permisos
- [ ] Probar con token de Admin (tiene todos los permisos)
- [ ] Probar con usuarios con permisos limitados
- [ ] Documentar los permisos del módulo

---

## 💡 Consejos Finales

1. **Usa nombres consistentes:** `modulo.accion` (singular, minúsculas)
   - ✅ `inventarios.view`, `usuarios.create`
   - ❌ `Inventario.Ver`, `user_create`

2. **Permisos granulares vs simples:**
   - Granular: `depositos.view`, `depositos.create`, `depositos.edit`, `depositos.delete`, `depositos.approve`
   - Simple: `depositos.read`, `depositos.write`
   - **Recomendación:** Usa granular para flexibilidad

3. **Logging de auditoría:**
   ```csharp
   if (!HasPermission("inventarios.delete"))
   {
       _logger.LogWarning("Usuario {User} intentó eliminar inventario sin permiso", 
           User.Identity?.Name);
       return Forbid();
   }
   ```

4. **Manejo de errores:**
   ```csharp
   return Forbid(); // 403 cuando no tiene permiso
   return Unauthorized(); // 401 cuando no está autenticado
   return NotFound(); // 404 cuando el recurso no existe
   ```

5. **Testing:**
   - Prueba siempre con token válido pero sin permisos (debe dar 403)
   - Prueba con token expirado (debe dar 401)
   - Prueba sin token (debe dar 401)

---

## 📞 Soporte

Para dudas sobre:
- **Permisos del Core:** Contactar al equipo Core
- **Active Directory:** Contactar a Sistemas
- **Desarrollo de módulos:** Consultar esta guía

---

**Última actualización:** Enero 2026  
**Versión del Core:** 1.0  
**Autor:** Equipo SCNexus Core
