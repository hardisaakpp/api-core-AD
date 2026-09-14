namespace SCNexus.Core.API.DTOs
{
    /// <summary>
    /// Proyección mínima de un acceso a almacén tal como viaja en el claim JWT <c>warehouse_access</c>.
    /// Deliberadamente NO es <see cref="WarehouseAccessInfo"/>: el cuerpo del login sigue llevando la
    /// información completa (nombre y bodega central de cada grupo) para el front, pero el token solo
    /// necesita lo que los backends leen para autorizar: código de tienda, tipo de acceso y, para
    /// Supervisor, la lista de almacenes. Ver <c>Services/WarehouseAccessClaimBuilder</c>.
    /// </summary>
    public sealed class WarehouseAccessClaim
    {
        public string CodAlmacen { get; set; } = string.Empty;
        public string AccesoAlmacen { get; set; } = string.Empty;
        public List<WarehouseGroupClaim> WarehouseGroups { get; set; } = new();
    }

    /// <summary>Un "grupo" del claim: solo los almacenes. Misma forma (warehouseGroups[].almacenes) que leen los módulos.</summary>
    public sealed class WarehouseGroupClaim
    {
        public List<string> Almacenes { get; set; } = new();
    }
}
