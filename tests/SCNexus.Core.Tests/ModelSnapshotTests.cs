using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SCNexus.Core.API.Data;
using Xunit;

namespace SCNexus.Core.Tests;

/// <summary>
/// El snapshot de migraciones debe coincidir con el modelo actual: si alguien cambia una entidad sin generar su
/// migración (o escribe una migración a mano y olvida el snapshot), el siguiente "migrations add" arrastraría cambios
/// ajenos. No necesita base de datos: solo construye los modelos.
/// </summary>
public class ModelSnapshotTests
{
    [Fact]
    public void ElSnapshotCoincideConElModeloActual()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=sin_conexion;Trusted_Connection=True;")
            .Options;
        using var context = new AppDbContext(options);

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var differ = context.GetService<IMigrationsModelDiffer>();

        var snapshot = migrationsAssembly.ModelSnapshot?.Model;
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        if (snapshot is not null) snapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

        var cambios = differ.GetDifferences(
            snapshot?.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.True(cambios.Count == 0,
            "El modelo tiene cambios sin migración: " + string.Join("; ", cambios.Select(c => c.GetType().Name)));
    }

    [Fact]
    public void LaMigracionDeDescripcionesDeRolEstaRegistrada()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=.;Database=x;").Options;
        using var context = new AppDbContext(options);
        var migraciones = context.GetService<IMigrationsAssembly>().Migrations.Keys;
        Assert.Contains("20261001180000_AddRoleDescriptions", migraciones);
    }
}
