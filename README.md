# SCNexus Core - Identity & Authentication API

> **Identity Provider centralizado para arquitectura de microservicios desacoplados**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![ASP.NET Identity](https://img.shields.io/badge/ASP.NET-Identity-green)](https://docs.microsoft.com/aspnet/core/security/authentication/identity)
[![JWT](https://img.shields.io/badge/Auth-JWT-orange)](https://jwt.io/)

---

## 🎯 Propósito

Este proyecto es el **Core de Autenticación** para el ecosistema SCNexus. Proporciona:

- ✅ Autenticación centralizada (local + Active Directory)
- ✅ Emisión de tokens JWT con roles y permisos
- ✅ Gestión de usuarios, roles y permisos
- ✅ API simple y desacoplada para módulos externos

**Principio fundamental:** El Core **solo autentica** y emite JWT. Los módulos externos validan el token localmente sin acoplar código.

---

## 📐 Arquitectura

```
┌─────────────────────────────────────┐
│      CORE / Identity API            │
│  (Autentica y emite JWT)            │
└──────────────┬──────────────────────┘
               │ JWT
               ▼
┌──────────────────────────────────────┐
│   MÓDULOS Independientes             │
│   - Depósitos                        │
│   - Contabilidad                     │
│   - Inventario                       │
│   - Reportes                         │
│   (Validan JWT localmente)           │
└──────────────────────────────────────┘
```

**Sin acoplamiento, sin dependencias cruzadas.**

Documentación completa: [`ARCHITECTURE.md`](./ARCHITECTURE.md)

---

## 🚀 Inicio Rápido

### 1. Requisitos

- .NET 8.0 SDK
- SQL Server (local o Docker)
- Visual Studio 2022 / VS Code / Rider

### 2. Configurar Base de Datos

Edita `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SCNexusCore;Trusted_Connection=True;"
  }
}
```

### 3. Aplicar Migraciones

```bash
cd SCNexus.Core.API
dotnet ef database update
```

### 4. Ejecutar

```bash
dotnet run
```

La API estará disponible en: `http://210.17.1.54:5053`

### 5. Probar

Ver ejemplos completos en [`TESTING.md`](./TESTING.md)

**Login rápido:**

```powershell
$response = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/login" `
    -Method Post `
    -Body (@{userName="admin"; password="Admin123!"; useActiveDirectory=$false} | ConvertTo-Json) `
    -ContentType "application/json"

$token = $response.token
Write-Host "Token: $token"
```

---

## 📚 Endpoints Principales

| Método | Endpoint           | Descripción                              | Auth  |
|--------|--------------------|------------------------------------------|-------|
| POST   | `/api/auth/login`  | Iniciar sesión y obtener JWT             | No    |
| GET    | `/api/auth/me`     | Información del usuario autenticado      | Sí    |
| POST   | `/api/auth/validate` | Validar un token JWT                    | No    |
| GET    | `/api/permissions` | Listar permisos                          | Admin |
| POST   | `/api/permissions` | Crear permiso                            | Admin |
| POST   | `/api/permissions/assign` | Asignar permiso a rol              | Admin |
| GET    | `/api/permissions/role/{roleName}` | Permisos de un rol      | Admin |

Documentación completa: [`TESTING.md`](./TESTING.md)

---

## 🔐 Credenciales por Defecto

El sistema crea automáticamente un usuario admin:

- **Username:** `admin`
- **Password:** `Admin123!`
- **Role:** `Admin`

---

## 🧩 Crear un Módulo Externo

Los módulos externos **no dependen** del Core. Solo validan el JWT localmente.

**Ejemplo:** Ver [`MODULE_EXAMPLE.md`](./MODULE_EXAMPLE.md) para un módulo completo de "Depósitos".

### Configuración Mínima (appsettings.json del módulo)

```json
{
  "Authentication": {
    "JwtKey": "LA_MISMA_SECRET_KEY_DEL_CORE",
    "JwtIssuer": "https://core-api.example.com",
    "JwtAudience": "https://core-api.example.com"
  }
}
```

### Validación en el Módulo (Program.cs)

```csharp
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Authentication:JwtKey"])
            )
        };
    });
```

### Verificar Permisos (Controller del módulo)

```csharp
[HttpGet]
[Authorize]
public IActionResult GetDepositos()
{
    var permissions = User.Claims
        .Where(c => c.Type == "permission")
        .Select(c => c.Value)
        .ToList();

    if (!permissions.Contains("depositos.view"))
        return Forbid();

    // Lógica del endpoint...
}
```

**Documentación completa:** [`MODULE_EXAMPLE.md`](./MODULE_EXAMPLE.md)

---

## 🔒 Sistema de Permisos

### Estructura

```
Permission
├── key: "depositos.view"
├── description: "Ver depósitos"
└── module: "depositos"

Role
└── Permissions []

User
└── Roles []
    └── Permissions []
```

### Crear Permisos

```bash
POST /api/permissions
{
  "key": "depositos.view",
  "description": "Ver depósitos",
  "module": "depositos"
}
```

### Asignar a Rol

```bash
POST /api/permissions/assign
{
  "roleName": "Contabilidad",
  "permissionId": 1
}
```

### Token JWT incluye Permisos

```json
{
  "sub": "user-id",
  "name": "contador1",
  "role": ["Contabilidad"],
  "permission": ["depositos.view", "depositos.create"],
  "exp": 1732506777
}
```

---

## 🗂️ Estructura del Proyecto

```
SCNexus.Core.API/
├── Controllers/
│   ├── AuthController.cs          # Login, /me, /validate
│   ├── PermissionsController.cs   # Gestión de permisos
│   ├── UsersController.cs         # CRUD de usuarios
│   └── AdminController.cs         # Sync LDAP
├── Models/
│   ├── AppUser.cs                 # Usuario (Identity)
│   ├── Permission.cs              # Permiso
│   └── RolePermission.cs          # Relación Role-Permission
├── Services/
│   ├── AuthService.cs             # Lógica de autenticación
│   ├── UserService.cs             # Lógica de usuarios
│   └── AdLdapSyncService.cs       # Sincronización AD/LDAP
├── Data/
│   └── AppDbContext.cs            # EF Core DbContext
├── DTOs/
│   └── AuthResponses.cs           # DTOs de respuestas
├── Helpers/
│   └── DataSeeder.cs              # Seedeo inicial
└── Program.cs                     # Configuración de la app
```

---

## 🔧 Configuración

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SCNexusCore;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "TU_SECRET_KEY_SEGURA_DE_AL_MENOS_32_CARACTERES",
    "Issuer": "https://core-api.example.com",
    "Audience": "https://core-api.example.com"
  },
  "Ldap": {
    "Host": "10.10.100.8",
    "Port": 389,
    "Domain": "sunsetcorp.local",
    "BindDn": "ldapconsulta@sunsetcorp.local",
    "BindPassword": "<bind-password>",
    "SearchBase": "DC=sunsetcorp,DC=local",
    "UserFilter": "(objectClass=user)",
    "DefaultRole": "User",
    "PageSize": 500,
    "NightlySync": {
      "Enabled": true,
      "Time": "02:30",
      "TimeZone": "America/Guayaquil",
      "DeactivateMissing": false
    }
  }
}
```

### Sincronización de usuarios con Active Directory

- **Manual:** `POST /api/Admin/sync-ldap` (rol Admin) inicia la sincronización en segundo plano y responde 202;
  `GET /api/Admin/sync-ldap/status` devuelve si está corriendo, el último resultado (procesados, creados,
  actualizados, desactivados, omitidos, errores, duración) y la próxima ejecución programada. Si ya hay una en
  curso responde 409.
- **Automática:** `AdUserNightlySyncHostedService` corre dentro del Core todas las noches a la hora de
  `Ldap:NightlySync:Time` en la zona `Ldap:NightlySync:TimeZone` (por defecto 02:30 América/Guayaquil; si el
  contenedor no tiene tzdata usa UTC-5 fijo). `Enabled=false` la apaga. No requiere cron ni scheduler externo.
- **Cómo sincroniza:** búsqueda paginada (`PageSize`, 500 por defecto) sobre `SearchBase` con el filtro
  `UserFilter` **y** `(objectCategory=person)` sin cuentas de máquina (`sAMAccountName` terminado en `$`).
  Crea usuarios nuevos con `UserName` = correo si existe, si no el login AD; actualiza correo, nombre, estado
  (`userAccountControl` deshabilitado → `IsActive=false`). Los usuarios que AD ya no devuelve **no** se tocan salvo
  `DeactivateMissing=true`, que los marca inactivos (nunca borra).
- Los errores por usuario (nombre inválido para Identity, duplicados) se registran en el log y cuentan en
  `errors`/`skipped` sin abortar la corrida.

---

## 🐳 Docker

### docker-compose.yml

```yaml
version: '3.8'

services:
  core-api:
    build: ./SCNexus.Core.API
    ports:
      - "5053:80"
    environment:
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=SCNexusCore;User=sa;Password=YourPassword123!
      - Jwt__Key=TU_SECRET_KEY_SEGURA
      - Jwt__Issuer=https://core-api.example.com
      - Jwt__Audience=https://core-api.example.com
    depends_on:
      - sql-server

  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword123!
    ports:
      - "1433:1433"
```

---

## 📖 Documentación Adicional

- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Arquitectura completa y principios de diseño
- **[TESTING.md](./TESTING.md)** - Ejemplos de testing de todos los endpoints
- **[MODULE_EXAMPLE.md](./MODULE_EXAMPLE.md)** - Ejemplo completo de módulo externo

---

## 🛠️ Tecnologías

- **ASP.NET Core 8.0** - Framework web
- **ASP.NET Identity** - Sistema de usuarios y roles
- **Entity Framework Core 8.0** - ORM
- **SQL Server** - Base de datos
- **JWT Bearer** - Autenticación con tokens
- **System.DirectoryServices.Protocols** - Integración con Active Directory
- **Swagger/OpenAPI** - Documentación de API

---

## 🔐 Seguridad

- ✅ JWT con firma HMAC-SHA256
- ✅ Tokens expiran en 3 horas (configurable)
- ✅ Soporte para HTTPS
- ✅ Validación de roles y permisos
- ✅ Integración con Active Directory
- ✅ Passwords hasheados con Identity

**Recomendaciones para producción:**
- Usar HTTPS en todos los endpoints
- Rotar la `Jwt:Key` periódicamente
- Implementar refresh tokens para sesiones largas
- Configurar CORS adecuadamente
- Auditar cambios en permisos

---

## 🤝 Contribución

Este proyecto sigue una arquitectura modular. Para agregar funcionalidad:

1. **Nuevos permisos:** Crear en `/api/permissions`
2. **Nuevos roles:** Usar ASP.NET Identity
3. **Nuevos módulos:** Ver `MODULE_EXAMPLE.md`

---

## 📝 Licencia

Privado - SCNexus © 2024

---

## 📞 Soporte

Para agregar más módulos o extender funcionalidad, consulta la documentación en:
- `ARCHITECTURE.md` - Principios de diseño
- `MODULE_EXAMPLE.md` - Crear nuevos módulos
- `TESTING.md` - Probar el sistema

---

**¡Arquitectura simple, desacoplada y escalable!** 🚀
