# Claim `warehouse_access` del JWT

Core emite un claim `warehouse_access` por cada acceso a almacén del usuario, con un JSON en camelCase.
Desde el 14/09/2026 ese JSON es la **proyección mínima** que los módulos necesitan para autorizar
(`Services/WarehouseAccessClaimBuilder`):

```json
{"codAlmacen":"RL-PSC","accesoAlmacen":"Supervisor","warehouseGroups":[{"almacenes":["OUT-LLG","RL-PSC","RL-PSP"]}]}
```

| Tipo de acceso | `warehouseGroups` en el token |
|---|---|
| `Administrador` | `[]` (los módulos conceden acceso total sin mirar grupos) |
| `Tienda` | `[]` (los módulos usan solo `codAlmacen`) |
| `Supervisor` | un único grupo con la **unión** de almacenes de todos sus grupos, sin duplicados |

No se serializan `id`, `name` ni `bodegaCentral` de los grupos: ningún módulo los lee del token. El
**cuerpo** del login (`UserInfo.WarehouseAccesses`) y `GET /api/Users/info/{id}` siguen devolviendo la
información completa; el front usa esa, no el claim.

## Por qué

Medición del 10/09/2026 en pruebas: el token de `admin` pesaba 6.768 bytes (103 permisos, 8 grupos
repetidos completos en cada acceso) y el de `sistemas` 2.724. nginx del front y Apache del host
cortan las cabeceras a 8 KB, así que a un administrador le bastaba un acceso más para no poder
autenticarse. La parte de almacenes era la que crecía con cada acceso; con la proyección mínima se
vuelve casi constante y el resto del token son los permisos.

## Compatibilidad

La forma JSON (nombres y anidamiento) no cambia, solo se omite lo que nadie lee y se aplanan los
grupos. Los cuatro backends que deserializan el claim siguen funcionando sin cambios:

| Backend | Qué lee |
|---|---|
| api-inventario (`CurrentUserContextMiddleware`) | `codAlmacen`, `accesoAlmacen`, `warehouseGroups[].almacenes` (Supervisor) |
| api-caja-chica (`CurrentUserContextMiddleware`) | ídem |
| api-transferencias-directas (`WarehouseAccessResolver`) | ídem, solo el primer claim |
| api-recepcion-mercaderia (`ClaimsCurrentUserContext`) | ídem; declara `bodegaCentral` en su modelo pero no lo usa |

Los demás backends solo leen `permission` y roles. Los tokens emitidos antes del despliegue siguen
siendo válidos hasta su vencimiento (3 h) y se leen igual.

Si un módulo nuevo necesita el nombre del grupo o la bodega central, debe pedirlos a Core
(`GET /api/Users/info/{id}`) y no al token: el token es para autorizar, no para transportar catálogos.

## Cómo medir

```bash
TOKEN=$(curl -s -X POST http://<core>/api/auth/login -H 'Content-Type: application/json' \
  -d '{"userName":"admin","password":"...","useActiveDirectory":false}' | jq -r .token)
echo -n "$TOKEN" | wc -c
```

Como red de seguridad adicional conviene subir el límite de cabecera a 16 KB en el nginx del front
(`large_client_header_buffers 4 16k;`) y en Apache del host (`LimitRequestFieldSize 16384`).
