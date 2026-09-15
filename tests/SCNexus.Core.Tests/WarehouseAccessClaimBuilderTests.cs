using System.Text.Json;
using SCNexus.Core.API.DTOs;
using SCNexus.Core.API.Services;
using Xunit;

namespace SCNexus.Core.Tests;

/// <summary>
/// El claim warehouse_access debe ser lo más pequeño posible sin cambiar la forma que leen los
/// módulos (codAlmacen, accesoAlmacen, warehouseGroups[].almacenes). Ver docs/JWT-WAREHOUSE-ACCESS.md.
/// </summary>
public sealed class WarehouseAccessClaimBuilderTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static WarehouseGroupInfo Group(string id, params string[] almacenes) => new()
    {
        Id = id,
        Name = $"Grupo {id}",
        BodegaCentral = "CD01",
        Almacenes = almacenes.ToList()
    };

    private static WarehouseAccessInfo Access(string codAlmacen, string acceso, params WarehouseGroupInfo[] groups) => new()
    {
        CodAlmacen = codAlmacen,
        AccesoAlmacen = acceso,
        WarehouseGroups = groups.ToList()
    };

    [Fact]
    public void Administrador_ViajaSinGrupos()
    {
        var claims = WarehouseAccessClaimBuilder.Build([Access("CD01", "Administrador", Group("1", "A", "B"), Group("2", "C"))]);

        var claim = Assert.Single(claims);
        Assert.Equal("CD01", claim.CodAlmacen);
        Assert.Equal("Administrador", claim.AccesoAlmacen);
        Assert.Empty(claim.WarehouseGroups);
    }

    [Fact]
    public void Tienda_ViajaSinGrupos()
    {
        var claims = WarehouseAccessClaimBuilder.Build([Access("OUT-LLG", "Tienda", Group("1", "A", "B"))]);

        var claim = Assert.Single(claims);
        Assert.Equal("OUT-LLG", claim.CodAlmacen);
        Assert.Empty(claim.WarehouseGroups);
    }

    [Fact]
    public void Supervisor_UneLosAlmacenesDeTodosSusGruposSinDuplicados()
    {
        var claims = WarehouseAccessClaimBuilder.Build(
        [
            Access("RL-PSC", "Supervisor",
                Group("1", "RL-PSC", "RL-PSP", " RL-REC2 "),
                Group("2", "rl-psp", "OUT-LLG", ""),
                Group("3"))
        ]);

        var claim = Assert.Single(claims);
        var group = Assert.Single(claim.WarehouseGroups);
        Assert.Equal(["OUT-LLG", "RL-PSC", "RL-PSP", "RL-REC2"], group.Almacenes);
    }

    [Fact]
    public void Supervisor_SinAlmacenesEnSusGrupos_ViajaSinGrupos_ComoHoy()
    {
        // Los módulos ya rechazan este caso ("Supervisor sin grupos de almacenes"); el token no debe
        // inventar nada, solo dejarlo igual de vacío que antes.
        var claims = WarehouseAccessClaimBuilder.Build([Access("RL-PSC", "Supervisor", Group("1"), Group("2", " ", ""))]);

        Assert.Empty(Assert.Single(claims).WarehouseGroups);
    }

    [Fact]
    public void Json_ConservaLaFormaQueLeenLosModulos_YNoLlevaCamposQueNadieUsa()
    {
        var claim = WarehouseAccessClaimBuilder.Build([Access("RL-PSC", "Supervisor", Group("7", "RL-PSC", "RL-PSP"))]).Single();

        var json = JsonSerializer.Serialize(claim, CamelCase);

        Assert.Contains("\"codAlmacen\":\"RL-PSC\"", json);
        Assert.Contains("\"accesoAlmacen\":\"Supervisor\"", json);
        Assert.Contains("\"warehouseGroups\":[{\"almacenes\":[\"RL-PSC\",\"RL-PSP\"]}]", json);
        Assert.DoesNotContain("\"id\"", json);
        Assert.DoesNotContain("\"name\"", json);
        Assert.DoesNotContain("bodegaCentral", json);
    }

    [Fact]
    public void Json_LoDeserializaElModeloQueUsanLosModulos_YProduceLosMismosAlmacenes()
    {
        var access = Access("RL-PSC", "Supervisor", Group("1", "RL-PSC", "RL-PSP"), Group("2", "RL-PSP", "OUT-LLG"));
        var json = JsonSerializer.Serialize(WarehouseAccessClaimBuilder.Build([access]).Single(), CamelCase);

        // Mismo modelo que api-inventario / api-caja-chica (con Id y Name declarados aunque no vengan).
        var parsed = JsonSerializer.Deserialize<ModuleWarehouseAccessClaim>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var fromSlimToken = parsed.WarehouseGroups.SelectMany(g => g.Almacenes).Select(a => a.ToUpperInvariant()).ToHashSet();
        var fromFullBody = access.WarehouseGroups.SelectMany(g => g.Almacenes).Select(a => a.ToUpperInvariant()).ToHashSet();
        Assert.Equal(fromFullBody, fromSlimToken);
        Assert.Equal("Supervisor", parsed.AccesoAlmacen);
        Assert.All(parsed.WarehouseGroups, g => Assert.Null(g.Id));
    }

    [Fact]
    public void Tamano_UnAdministradorConOchoGruposPesaUnaFraccionDelClaimAnterior()
    {
        var groups = Enumerable.Range(1, 8)
            .Select(i => Group(i.ToString(), Enumerable.Range(1, 15).Select(j => $"ALM-{i:00}-{j:00}").ToArray()))
            .ToArray();
        var accesses = new[]
        {
            Access("CD01", "Administrador", groups),
            Access("OUT-LLG", "Tienda", groups),
            Access("RL-PSC", "Supervisor", groups)
        };

        var before = accesses.Sum(a => JsonSerializer.Serialize(a, CamelCase).Length);
        var after = WarehouseAccessClaimBuilder.Build(accesses).Sum(c => JsonSerializer.Serialize(c, CamelCase).Length);

        Assert.True(after < before / 3, $"el claim reducido pesa {after} bytes frente a {before} del anterior");
    }

    private sealed class ModuleWarehouseAccessClaim
    {
        public string CodAlmacen { get; set; } = string.Empty;
        public string AccesoAlmacen { get; set; } = string.Empty;
        public List<ModuleWarehouseGroupClaim> WarehouseGroups { get; set; } = [];
    }

    private sealed class ModuleWarehouseGroupClaim
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<string> Almacenes { get; set; } = [];
    }
}
