using SCNexus.Core.API.Controllers;
using Xunit;

namespace SCNexus.Core.Tests;

public class RolesControllerTests
{
    [Theory]
    [InlineData("Desarrollador")]
    [InlineData("Dev Xia")]
    [InlineData("Jefe-de_Tienda.2")]
    [InlineData("Contabilidad Ñandú")]
    [InlineData("A1")]
    public void Validate_NombresValidos_SinError(string nombre)
    {
        Assert.Null(RolesController.Validate(nombre, null, out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("-guion-inicial")]
    [InlineData(" _guion_bajo")]
    [InlineData("rol<script>")]
    [InlineData("rol/otro")]
    [InlineData("rol;drop")]
    public void Validate_NombresInvalidos_DevuelveMensaje(string nombre)
    {
        Assert.NotNull(RolesController.Validate(nombre, null, out _, out _));
    }

    [Fact]
    public void Validate_LimitaLongitudDeNombreYDescripcion()
    {
        Assert.Null(RolesController.Validate(new string('a', RolesController.MaxNameLength), new string('d', 500), out _, out _));
        Assert.NotNull(RolesController.Validate(new string('a', RolesController.MaxNameLength + 1), null, out _, out _));
        Assert.NotNull(RolesController.Validate("Rol", new string('d', 501), out _, out _));
    }

    [Fact]
    public void Validate_RecortaEspaciosYNormalizaDescripcionNula()
    {
        Assert.Null(RolesController.Validate("  Dev Xia  ", null, out var nombre, out var descripcion));
        Assert.Equal("Dev Xia", nombre);
        Assert.Equal(string.Empty, descripcion);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("User")]
    [InlineData("Contabilidad")]
    [InlineData("Gerente")]
    public void SystemRoles_ProtegeLosRolesDelSistemaSinImportarMayusculas(string nombre)
    {
        Assert.Contains(nombre, RolesController.SystemRoles);
    }

    [Fact]
    public void SystemRoles_NoIncluyeRolesCreadosPorElUsuario()
    {
        Assert.DoesNotContain("Dev Xia", RolesController.SystemRoles);
    }
}
