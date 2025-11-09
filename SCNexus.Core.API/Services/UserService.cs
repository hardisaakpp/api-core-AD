using Microsoft.AspNetCore.Identity;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Services
{
    public class UserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserService(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<List<AppUser>> GetAllUsersAsync() => _userManager.Users.ToList();

        public async Task<AppUser?> GetByIdAsync(string id) => await _userManager.FindByIdAsync(id);

        public async Task<IdentityResult> CreateUserAsync(AppUser user, string password, string role)
        {
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded) return result;

            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

            await _userManager.AddToRoleAsync(user, role);
            return result;
        }

        public async Task<IdentityResult> UpdateUserAsync(AppUser user) => await _userManager.UpdateAsync(user);

        public async Task<IdentityResult> DeleteUserAsync(AppUser user) => await _userManager.DeleteAsync(user);
    }
}
