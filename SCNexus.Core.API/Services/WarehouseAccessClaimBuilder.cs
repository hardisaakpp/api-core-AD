using SCNexus.Core.API.DTOs;

namespace SCNexus.Core.API.Services
{
    /// <summary>
    /// Convierte los accesos a almacén del usuario en los claims <c>warehouse_access</c> del JWT con el
    /// tamaño mínimo que los módulos necesitan.
    ///
    /// Motivo: el token de un administrador medía 6,7 KB (medición del 10/09/2026) porque cada claim
    /// repetía los grupos completos (id, nombre, bodega central y la lista de almacenes de cada grupo)
    /// y nginx/Apache cortan las cabeceras a 8 KB. Los cuatro backends que leen este claim
    /// (inventario, caja chica, transferencias directas y recepción) solo usan <c>codAlmacen</c>,
    /// <c>accesoAlmacen</c> y <c>warehouseGroups[].almacenes</c>, y este último únicamente cuando el
    /// acceso es Supervisor. Por eso:
    ///   - Administrador y Tienda viajan sin grupos.
    ///   - Supervisor viaja con un único grupo que une los almacenes de todos sus grupos, sin duplicados.
    ///   - Nunca se serializan id, nombre ni bodega central.
    /// La forma JSON (nombres de propiedad y anidamiento) se mantiene, así que ningún módulo cambia.
    /// El cuerpo del login (<c>UserInfo.WarehouseAccesses</c>) no se toca.
    /// </summary>
    public static class WarehouseAccessClaimBuilder
    {
        public const string ClaimType = "warehouse_access";
        public const string SupervisorAccess = "Supervisor";

        public static List<WarehouseAccessClaim> Build(IEnumerable<WarehouseAccessInfo> accesses)
        {
            var result = new List<WarehouseAccessClaim>();

            foreach (var access in accesses)
            {
                var claim = new WarehouseAccessClaim
                {
                    CodAlmacen = access.CodAlmacen,
                    AccesoAlmacen = access.AccesoAlmacen
                };

                if (IsSupervisor(access.AccesoAlmacen))
                {
                    var almacenes = FlattenAlmacenes(access.WarehouseGroups);
                    if (almacenes.Count > 0)
                    {
                        claim.WarehouseGroups.Add(new WarehouseGroupClaim { Almacenes = almacenes });
                    }
                }

                result.Add(claim);
            }

            return result;
        }

        private static bool IsSupervisor(string? accesoAlmacen)
            => string.Equals(accesoAlmacen?.Trim(), SupervisorAccess, StringComparison.OrdinalIgnoreCase);

        /// <summary>Unión de almacenes de todos los grupos: sin vacíos, sin duplicados (ignorando mayúsculas), orden estable.</summary>
        private static List<string> FlattenAlmacenes(IEnumerable<WarehouseGroupInfo> groups)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var almacenes = new List<string>();

            foreach (var almacen in groups.SelectMany(g => g.Almacenes ?? new List<string>()))
            {
                if (string.IsNullOrWhiteSpace(almacen))
                {
                    continue;
                }

                var trimmed = almacen.Trim();
                if (seen.Add(trimmed))
                {
                    almacenes.Add(trimmed);
                }
            }

            almacenes.Sort(StringComparer.Ordinal);
            return almacenes;
        }
    }
}
