using Decatron.Services;
using Decatron.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Decatron.Default.Controllers
{
    [ApiController]
    [Route("api/user")]
    [Authorize]
    public class UserPermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<UserPermissionsController> _logger;

        public UserPermissionsController(
            IPermissionService permissionService,
            ILogger<UserPermissionsController> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        private long GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdClaim, out var userId))
                return userId;
            throw new UnauthorizedAccessException("User not found");
        }

        private long GetChannelOwnerId()
        {
            var channelOwnerIdClaim = User.FindFirst("ChannelOwnerId")?.Value;
            if (long.TryParse(channelOwnerIdClaim, out var channelOwnerId))
                return channelOwnerId;

            return GetUserId();
        }

        /// <summary>
        /// Obtiene los permisos del usuario actual
        /// </summary>
        [HttpGet("permissions")]
        public async Task<IActionResult> GetUserPermissions()
        {
            try
            {
                var userId = GetUserId();
                var channelOwnerId = GetChannelOwnerId();

                var accessLevel = await _permissionService.GetUserAccessLevelAsync(userId, channelOwnerId);
                var isOwner = await _permissionService.IsChannelOwnerAsync(userId, channelOwnerId);

                return Ok(new
                {
                    success = true,
                    permissions = new
                    {
                        userId = userId,
                        channelOwnerId = channelOwnerId,
                        accessLevel = accessLevel ?? "none",
                        isOwner = isOwner,
                        sections = new
                        {
                            commands = await _permissionService.CanAccessAsync(userId, channelOwnerId, "commands"),
                            microcommands = await _permissionService.CanAccessAsync(userId, channelOwnerId, "microcommands"),
                            overlays = await _permissionService.CanAccessAsync(userId, channelOwnerId, "overlays"),
                            userManagement = await _permissionService.CanAccessAsync(userId, channelOwnerId, "user_management")
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos del usuario");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene las cuentas disponibles para el usuario (para cambio de canal)
        /// </summary>
        [HttpGet("available-accounts")]
        public async Task<IActionResult> GetAvailableAccounts()
        {
            try
            {
                var userId = GetUserId();
                var accounts = await _permissionService.GetUserAvailableAccountsAsync(userId);

                return Ok(new
                {
                    success = true,
                    accounts = accounts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo cuentas disponibles del usuario");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }



        /// <summary>
        /// Obtiene todos los usuarios con acceso al canal (solo control total)
        /// </summary>
        [HttpGet("channel-users")]
        [RequirePermission("user_management", "control_total")]
        public async Task<IActionResult> GetChannelUsers()
        {
            try
            {
                var channelOwnerId = GetChannelOwnerId();
                var users = await _permissionService.GetChannelUsersAsync(channelOwnerId);

                return Ok(new
                {
                    success = true,
                    users = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo usuarios del canal");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }
    }
}