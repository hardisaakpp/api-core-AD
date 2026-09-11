using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCNexus.Core.API.Models;
using SCNexus.Core.API.Services;
using SCNexus.Core.API.DTOs;
using System.Security.Claims;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Controller-level authorization removed]

    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public class AssignRoleRequest { public string? Role { get; set; } }

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        // GET: api/Users/list — minimal list for comboboxes (id, fullName, email)
        [HttpGet("list")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetList()
        {
            var users = await _userService.GetUsersForComboboxAsync();
            return Ok(users);
        }

        // GET: api/Users?page=1&pageSize=20&search=carlos
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (users, total) = await _userService.GetPagedUsersAsync(page, pageSize, search);
            return Ok(new { data = users, total });
        }

        // GET: api/Users/role/{roleName}
        [HttpGet("role/{roleName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetByRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return BadRequest("Role requerido");
            var users = await _userService.GetUsersInRoleAsync(roleName);
            return Ok(users);
        }

        // GET: api/Users/{id}/roles
        [HttpGet("{id}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRoles(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userService.GetUserRolesAsync(user);
            return Ok(roles);
        }

        // POST: api/Users/{id}/roles
        // Body: { "role": "Admin" }
        [HttpPost("{id}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Role))
                return BadRequest("Role requerido");

            var result = await _userService.AddRoleToUserAsync(id, req.Role);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok();
        }

        // DELETE: api/Users/{id}/roles/{roleName}
        [HttpDelete("{id}/roles/{roleName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveRole(string id, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest("Role requerido");

            var result = await _userService.RemoveRoleFromUserAsync(id, roleName);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok();
        }

        // GET: api/Users/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // POST: api/Users?password=12345&role=Admin
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] AppUser user, [FromQuery] string password, [FromQuery] string role)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role))
                return BadRequest("Password y Role son requeridos");

            var result = await _userService.CreateUserAsync(user, password, role);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(user);
        }

        // PUT: api/Users/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
        {
            var existingUser = await _userService.GetByIdAsync(id);
            if (existingUser == null) return NotFound();
            // Solo actualizar campos permitidos
            if (!string.IsNullOrWhiteSpace(request.UserName)) existingUser.UserName = request.UserName;
            if (!string.IsNullOrWhiteSpace(request.Email)) existingUser.Email = request.Email;
            if (!string.IsNullOrWhiteSpace(request.FullName)) existingUser.FullName = request.FullName;
            if (request.IsActive.HasValue) existingUser.IsActive = request.IsActive.Value;

            var result = await _userService.UpdateUserAsync(existingUser);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(existingUser);
        }

        // DELETE: api/Users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userService.DeleteUserAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok();
        }

        // PUT: api/Users/me
        // Update own profile (authenticated users)
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest request)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();

            var user = await _userService.GetByUserNameAsync(userName);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(request.UserName)) user.UserName = request.UserName;
            if (!string.IsNullOrWhiteSpace(request.Email)) user.Email = request.Email;
            if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName;

            var result = await _userService.UpdateUserAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(user);
        }

        // POST: api/Users/me/password
        // Change own password
        [HttpPost("me/password")]
        [Authorize]
        public async Task<IActionResult> ChangeMyPassword([FromBody] DTOs.ChangePasswordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.OldPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest("OldPassword and NewPassword are required");

            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();

            var user = await _userService.GetByUserNameAsync(userName);
            if (user == null) return NotFound();

            var result = await _userService.ChangePasswordAsync(user.Id, req.OldPassword, req.NewPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok();
        }

        // POST: api/Users/{id}/password (admin)
        [HttpPost("{id}/password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminSetPassword(string id, [FromBody] DTOs.AdminSetPasswordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.NewPassword)) return BadRequest("NewPassword required");

            var result = await _userService.ResetPasswordByAdminAsync(id, req.NewPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok();
        }

        public class UpdateAvatarRequest { public string? Emoji { get; set; } }

        // GET: api/Users/me/avatar
        [HttpGet("me/avatar")]
        [Authorize]
        public async Task<IActionResult> GetMyAvatar()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();

            var user = await _userService.GetByUserNameAsync(userName);
            if (user == null) return NotFound();

            return Ok(new { emoji = user.AvatarEmoji });
        }

        // PUT: api/Users/me/avatar
        // Body: { "emoji": "😀" }
        [HttpPut("me/avatar")]
        [Authorize]
        public async Task<IActionResult> UpdateMyAvatar([FromBody] UpdateAvatarRequest request)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();

            var user = await _userService.GetByUserNameAsync(userName);
            if (user == null) return NotFound();

            user.AvatarEmoji = request?.Emoji;
            var result = await _userService.UpdateUserAsync(user);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new { emoji = user.AvatarEmoji });
        }

        // GET: api/Users/search?q=jper&limit=10
        // Autocomplete for any authenticated user: matches user name, full name or email of active
        // users and returns only basic public fields. Inventory uses it to pick pickers/recount
        // participants without knowing how the account is registered (login vs. email).
        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10)
        {
            var term = q?.Trim() ?? string.Empty;
            if (term.Length < 2) return Ok(Array.Empty<object>());

            limit = Math.Clamp(limit, 1, 20);
            var (users, _) = await _userService.GetPagedUsersAsync(1, limit, term);

            var result = users
                .Where(u => u.IsActive)
                .Select(u => new
                {
                    id = u.Id,
                    userName = u.UserName,
                    fullName = u.FullName,
                    email = u.Email
                });

            return Ok(result);
        }

        // GET: api/Users/info/{id}
        // Get basic user info (requires authentication, but not Admin role).
        // Accepts the CORE id (GUID) or the network/AD user name: inventory assigns pickers and
        // recount participants by AD login, and needs to confirm the user exists before saving it.
        [HttpGet("info/{id}")]
        [Authorize]
        public async Task<IActionResult> GetUserInfo(string id)
        {
            var user = await _userService.GetByIdAsync(id)
                       ?? await _userService.GetByUserNameAsync(id);
            if (user == null) return NotFound();

            // Return only public/basic information
            var userInfo = new
            {
                id = user.Id,
                userName = user.UserName,
                fullName = user.FullName,
                email = user.Email
            };

            return Ok(userInfo);
        }
    }
}
