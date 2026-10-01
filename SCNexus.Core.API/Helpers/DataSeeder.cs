using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Helpers
{
    /// <summary>
    /// Seedeo inicial de datos para el sistema
    /// </summary>
    public static class DataSeeder
    {
        /// <summary>
        /// Crear roles, permisos y usuario admin inicial
        /// </summary>
        public static async Task SeedAsync(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // 1. Crear roles si no existen
            string[] roles = { "Admin", "User", "Contabilidad", "Gerente" };
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Crear permisos base (módulos de ejemplo)
            var permissions = new List<Permission>
            {
                // Permisos de usuarios (Core)
                new Permission { Key = "users.view", Description = "Ver usuarios", Module = "core" },
                new Permission { Key = "users.create", Description = "Crear usuarios", Module = "core" },
                new Permission { Key = "users.edit", Description = "Editar usuarios", Module = "core" },
                new Permission { Key = "users.delete", Description = "Eliminar usuarios", Module = "core" },
                
                // Permisos de roles (Core)
                new Permission { Key = "roles.view", Description = "Ver roles", Module = "core" },
                new Permission { Key = "roles.manage", Description = "Gestionar roles", Module = "core" },
                
                // Permisos de permisos (Core)
                new Permission { Key = "permissions.view", Description = "Ver permisos", Module = "core" },
                new Permission { Key = "permissions.manage", Description = "Gestionar permisos", Module = "core" },
                
                // Permisos del módulo Depósitos (ejemplo)
                new Permission { Key = "depositos.view", Description = "Ver depósitos", Module = "depositos" },
                new Permission { Key = "depositos.create", Description = "Crear depósitos", Module = "depositos" },
                new Permission { Key = "depositos.edit", Description = "Editar depósitos", Module = "depositos" },
                new Permission { Key = "depositos.delete", Description = "Eliminar depósitos", Module = "depositos" },
                new Permission { Key = "depositos.approve", Description = "Aprobar depósitos", Module = "depositos" },
                
                // Permisos del módulo Contabilidad (ejemplo)
                new Permission { Key = "contabilidad.view", Description = "Ver registros contables", Module = "contabilidad" },
                new Permission { Key = "contabilidad.create", Description = "Crear asientos", Module = "contabilidad" },
                new Permission { Key = "contabilidad.close", Description = "Cerrar períodos", Module = "contabilidad" },
                
                // Permisos del módulo Reportes (ejemplo)
                new Permission { Key = "reportes.view", Description = "Ver reportes", Module = "reportes" },
                new Permission { Key = "reportes.export", Description = "Exportar reportes", Module = "reportes" },

                // Permisos del módulo ODC SAP (api-odc-mba3)
                new Permission { Key = "odc.view", Description = "Ver Gestión ODC SAP", Module = "odc" },
                new Permission { Key = "odcsap.view", Description = "ODC SAP - funcionalidades de El Rayo", Module = "odc" },
                new Permission { Key = "mabel.view", Description = "ODC SAP - funcionalidades de Mabel Trading", Module = "odc" }
            };

            var existingPermissionKeys = await context.Permissions
                .Select(p => p.Key)
                .ToListAsync();

            var existingPermissionKeySet = new HashSet<string>(existingPermissionKeys);
            var missingPermissions = permissions
                .Where(p => !existingPermissionKeySet.Contains(p.Key))
                .ToList();

            if (missingPermissions.Count > 0)
            {
                context.Permissions.AddRange(missingPermissions);
            }

            await context.SaveChangesAsync();

            // 3. Asignar TODOS los permisos al rol Admin
            var adminRole = await roleManager.FindByNameAsync("Admin");
            if (adminRole != null && !string.IsNullOrWhiteSpace(adminRole.Id))
            {
                var allPermissions = await context.Permissions.ToListAsync();
                var existingAdminPermissionIds = await context.RolePermissions
                    .Where(rp => rp.RoleId == adminRole.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();

                var existingAdminPermissionIdSet = new HashSet<int>(existingAdminPermissionIds);
                var adminRolePermissionsToAdd = allPermissions
                    .Where(perm => !existingAdminPermissionIdSet.Contains(perm.Id))
                    .Select(perm => new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = perm.Id
                    })
                    .ToList();

                if (adminRolePermissionsToAdd.Count > 0)
                {
                    context.RolePermissions.AddRange(adminRolePermissionsToAdd);
                }
            }

            // 4. Asignar permisos específicos a otros roles
            
            // Contabilidad: solo ver y crear en contabilidad y depósitos
            var contabilidadRole = await roleManager.FindByNameAsync("Contabilidad");
            if (contabilidadRole != null && !string.IsNullOrWhiteSpace(contabilidadRole.Id))
            {
                var contabilidadPermKeys = new List<string>
                { 
                    "contabilidad.view", "contabilidad.create",
                    "depositos.view", "depositos.create"
                };
                
                var contabilidadPerms = await context.Permissions
                    .Where(p => contabilidadPermKeys.Contains(p.Key))
                    .ToListAsync();

                var existingContabilidadPermissionIds = await context.RolePermissions
                    .Where(rp => rp.RoleId == contabilidadRole.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();

                var existingContabilidadPermissionIdSet = new HashSet<int>(existingContabilidadPermissionIds);
                var contabilidadRolePermissionsToAdd = contabilidadPerms
                    .Where(perm => !existingContabilidadPermissionIdSet.Contains(perm.Id))
                    .Select(perm => new RolePermission
                    {
                        RoleId = contabilidadRole.Id,
                        PermissionId = perm.Id
                    })
                    .ToList();

                if (contabilidadRolePermissionsToAdd.Count > 0)
                {
                    context.RolePermissions.AddRange(contabilidadRolePermissionsToAdd);
                }
            }

            // Gerente: ver todo, aprobar depósitos, cerrar períodos
            var gerenteRole = await roleManager.FindByNameAsync("Gerente");
            if (gerenteRole != null && !string.IsNullOrWhiteSpace(gerenteRole.Id))
            {
                var gerentePermKeys = new List<string>
                { 
                    "depositos.view", "depositos.approve",
                    "contabilidad.view", "contabilidad.close",
                    "reportes.view", "reportes.export"
                };
                
                var gerentePerms = await context.Permissions
                    .Where(p => gerentePermKeys.Contains(p.Key))
                    .ToListAsync();

                var existingGerentePermissionIds = await context.RolePermissions
                    .Where(rp => rp.RoleId == gerenteRole.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();

                var existingGerentePermissionIdSet = new HashSet<int>(existingGerentePermissionIds);
                var gerenteRolePermissionsToAdd = gerentePerms
                    .Where(perm => !existingGerentePermissionIdSet.Contains(perm.Id))
                    .Select(perm => new RolePermission
                    {
                        RoleId = gerenteRole.Id,
                        PermissionId = perm.Id
                    })
                    .ToList();

                if (gerenteRolePermissionsToAdd.Count > 0)
                {
                    context.RolePermissions.AddRange(gerenteRolePermissionsToAdd);
                }
            }

            await context.SaveChangesAsync();

            // 5. Crear usuario admin si no existe
            var adminUser = await userManager.FindByNameAsync("admin");
            if (adminUser == null)
            {
                adminUser = new AppUser
                {
                    UserName = "admin",
                    Email = "admin@scnexus.local",
                    FullName = "Administrator",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}
