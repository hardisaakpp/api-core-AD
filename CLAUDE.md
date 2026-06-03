# SCNexus Core — Instrucciones para Claude

## REGLA CRITICA: Servidores de Base de Datos

| Servidor | Rol | Lectura | Escritura desde dev |
|---|---|---|---|
| `210.17.1.54` | **DESARROLLO / PRUEBAS** | ✅ | ✅ |
| `192.168.2.211` | **PRODUCCION** | ✅ (solo lectura) | ❌ JAMAS |

**`192.168.2.211` es producción. NUNCA configurar escritura hacia ese servidor desde entornos de desarrollo o pruebas.**

### Bases de datos por servidor

- `210.17.1.54` → `SCNexusDB` — base de datos principal de la aplicación (EF Core + Identity). Lectura y escritura.
- `192.168.2.211` → `SCMaestros` — datos maestros legados (almacenes, artículos, etc.). Solo lectura. Queries con ADO.NET raw, no EF navigation (es una DB externa al modelo EF).

### Archivos de configuración

- `appsettings.json` — base, `DefaultConnection` apunta a `210.17.1.54`
- `appsettings.Development.json` — override dev, también apunta a `210.17.1.54`
- `.env` — variables de entorno para Docker en el servidor dev, también `210.17.1.54`

Si en cualquier momento `DefaultConnection` aparece apuntando a `192.168.2.211`, es un error — corregir de inmediato.

## Stack

- **Backend**: ASP.NET Core 8 Web API, EF Core, SQL Server, JWT auth, LDAP (sunsetcorp.local)
- **Frontend**: `scnexus-front` — Vue 3 + Vite + Tailwind CSS
- **CORS**: permite `localhost`, `192.168.x.x`, `210.17.1.x`, `172.30.x.x`

## Queries cross-DB

Las consultas a `SCMaestros` deben hacerse con ADO.NET raw (no EF), ya que esa DB no está en el modelo de EF Core.
