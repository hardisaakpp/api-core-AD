using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCNexus.Core.API.Models;
using SCNexus.Core.API.Services;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Solo Admin puede gestionar usuarios
    //[Authorize] // Solo requiere estar autenticado, sin importar rol
    //[AllowAnonymous] // Permite cualquier usuario (incluso sin token)

    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        // GET: api/Users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // POST: api/Users?password=12345&role=Admin
        [HttpPost]
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
        public async Task<IActionResult> Update(string id, [FromBody] AppUser user)
        {
            var existingUser = await _userService.GetByIdAsync(id);
            if (existingUser == null) return NotFound();

            // Solo actualizar campos permitidos
            existingUser.UserName = user.UserName;
            existingUser.Email = user.Email;

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
    }
}
