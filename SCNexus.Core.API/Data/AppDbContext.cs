using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata; // 👈 IMPORTANTE
using SCNexus.Core.API.Models;
using System.Text;

namespace SCNexus.Core.API.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<AspNetUserWarehouseAccess> UserWarehouseAccesses { get; set; } = null!;
        public DbSet<WarehouseGroup> WarehouseGroups { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar relación muchos a muchos entre Roles y Permissions
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // Índice único en Permission.Key
            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Key)
                .IsUnique();

            // UserWarehouseAccess: FK hacia AspNetUsers, sin FK cross-database hacia SCMaestros
            modelBuilder.Entity<AspNetUserWarehouseAccess>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Evita duplicados: un usuario no puede tener el mismo almacén dos veces
            modelBuilder.Entity<AspNetUserWarehouseAccess>()
                .HasIndex(a => new { a.UserId, a.CodAlmacen })
                .IsUnique();

            // WarehouseGroup.Almacenes almacenado como JSON en columna nvarchar(max)
            modelBuilder.Entity<WarehouseGroup>()
                .Property(g => g.Almacenes)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                )
                .HasColumnType("nvarchar(max)");

            // Aplica snake_case a tablas, columnas, claves e índices
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(ToSnakeCase(entity.GetTableName()!));

                foreach (var property in entity.GetProperties())
                {
                    var tableId = StoreObjectIdentifier.Table(entity.GetTableName()!, null);
                    var currentName = property.GetColumnName(tableId);
                    if (currentName != null)
                        property.SetColumnName(ToSnakeCase(currentName));
                }

                foreach (var key in entity.GetKeys())
                    key.SetName(ToSnakeCase(key.GetName()!));

                foreach (var fk in entity.GetForeignKeys())
                    fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));

                foreach (var index in entity.GetIndexes())
                    index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }

        private static string ToSnakeCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var builder = new StringBuilder(input.Length + 10);
            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        builder.Append('_');
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }
    }
}
