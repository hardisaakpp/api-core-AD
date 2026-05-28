using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        public async Task<List<UserComboboxItem>> GetUsersForComboboxAsync()
        {
            return await _userManager.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserComboboxItem { Id = u.Id, FullName = u.FullName, Email = u.Email })
                .ToListAsync();
        }

        public record UserComboboxItem
        {
            public string Id { get; init; } = string.Empty;
            public string FullName { get; init; } = string.Empty;
            public string? Email { get; init; }
        }

        public async Task<(List<AppUser> Users, int Total)> GetPagedUsersAsync(int page, int pageSize, string? search)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)));
            }

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (users, total);
        }

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
        
        public async Task<List<AppUser>> GetUsersInRoleAsync(string role)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
            return usersInRole.ToList();
        }

        public async Task<IList<string>> GetUserRolesAsync(AppUser user)
        {
            return await _userManager.GetRolesAsync(user);
        }

        public async Task<IdentityResult> AddRoleToUserAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });

            if (!await _roleManager.RoleExistsAsync(role))
            {
                var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(role));
                if (!createRoleResult.Succeeded) return createRoleResult;
            }

            var result = await _userManager.AddToRoleAsync(user, role);
            return result;
        }

        public async Task<AppUser?> GetByUserNameAsync(string userName) => await _userManager.FindByNameAsync(userName);

        public async Task<IdentityResult> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });

            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
            return result;
        }

        public async Task<IdentityResult> ResetPasswordByAdminAsync(string userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result;
        }

        public async Task<IdentityResult> RemoveRoleFromUserAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });

            var result = await _userManager.RemoveFromRoleAsync(user, role);
            return result;
        }
    }
}
