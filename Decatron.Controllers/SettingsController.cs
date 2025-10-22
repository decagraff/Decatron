﻿using Decatron.Core.Interfaces;
using Decatron.Core.Models;
using Decatron.Services;
using Decatron.Data;
using Decatron.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Decatron.Controllers
{
    [ApiController]
    [Route("api/settings")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;
        private readonly DecatronDbContext _dbContext;
        private readonly ILogger<SettingsController> _logger;
        private readonly TwitchBotService _botService;

        public SettingsController(
            ISettingsService settingsService,
            IAuthService authService,
            IPermissionService permissionService,
            DecatronDbContext dbContext,
            ILogger<SettingsController> logger,
            TwitchBotService botService)
        {
            _settingsService = settingsService;
            _authService = authService;
            _permissionService = permissionService;
            _dbContext = dbContext;
            _logger = logger;
            _botService = botService;
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

        [HttpPost("update")]
        public async Task<IActionResult> UpdateSettings([FromBody] SystemSettingsDto dto)
        {
            try
            {
                var userId = GetUserId();
                var channelOwnerId = GetChannelOwnerId();

                // Solo verificar permisos si no es el mismo usuario (es decir, si es un editor)
                if (userId != channelOwnerId)
                {
                    if (!await _permissionService.HasPermissionLevelAsync(userId, channelOwnerId, "control_total"))
                    {
                        return Forbid("Solo usuarios con control total pueden cambiar la configuración del bot");
                    }
                }

                var settings = await _settingsService.GetSettingsByUserIdAsync(channelOwnerId);
                if (settings == null)
                {
                    return NotFound(new { success = false, message = "Settings not found" });
                }

                settings.BotEnabled = dto.BotEnabled;
                settings.UpdatedAt = DateTime.UtcNow;

                await _settingsService.UpdateSettingsAsync(settings);

                _logger.LogInformation($"Settings updated by user {userId} for channel {channelOwnerId} - Bot: {(dto.BotEnabled ? "Enabled" : "Disabled")}");

                return Ok(new
                {
                    success = true,
                    message = $"Configuración guardada correctamente. Bot {(dto.BotEnabled ? "activado" : "desactivado")}.",
                    data = settings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("add-access")]
        public async Task<IActionResult> AddUserAccess([FromBody] AddUserAccessDto dto)
        {
            try
            {
                _logger.LogInformation($"AddUserAccess called");
                _logger.LogInformation($"Request body: AuthorizedUserId={dto?.AuthorizedUserId}, PermissionLevel={dto?.PermissionLevel}");

                if (dto == null)
                {
                    return BadRequest(new { success = false, message = "No se recibieron datos" });
                }

                if (string.IsNullOrEmpty(dto.AuthorizedUserId))
                {
                    return BadRequest(new { success = false, message = "ID de usuario es requerido" });
                }

                if (string.IsNullOrEmpty(dto.PermissionLevel))
                {
                    return BadRequest(new { success = false, message = "Nivel de permisos es requerido" });
                }

                var userId = GetUserId();
                var channelOwnerId = GetChannelOwnerId();

                // Buscar usuario por UniqueId directamente en DbContext
                var authorizedUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.UniqueId == dto.AuthorizedUserId && u.IsActive);

                if (authorizedUser == null)
                {
                    _logger.LogError($"User not found with UniqueId: {dto.AuthorizedUserId}");
                    return BadRequest(new { success = false, message = $"Usuario con ID {dto.AuthorizedUserId} no encontrado" });
                }

                _logger.LogInformation($"Found authorized user: {authorizedUser.Login} (ID: {authorizedUser.Id})");

                // Verificar niveles de permisos válidos
                var validLevels = new[] { "commands", "moderation", "control_total" };
                if (!validLevels.Contains(dto.PermissionLevel))
                {
                    return BadRequest(new { success = false, message = $"Nivel de permisos inválido: '{dto.PermissionLevel}'" });
                }

                // Verificar si ya existe un permiso para este usuario
                var existingPermission = await _dbContext.UserChannelPermissions
                    .FirstOrDefaultAsync(p => p.ChannelOwnerId == channelOwnerId &&
                                             p.GrantedUserId == authorizedUser.Id);

                if (existingPermission != null)
                {
                    _logger.LogInformation($"Updating existing permission for user {authorizedUser.Id}");
                    existingPermission.AccessLevel = dto.PermissionLevel;
                    existingPermission.IsActive = true;
                    existingPermission.UpdatedAt = DateTime.UtcNow;
                    existingPermission.GrantedBy = userId;
                }
                else
                {
                    _logger.LogInformation($"Creating new permission for user {authorizedUser.Id}");
                    var newPermission = new UserChannelPermissions
                    {
                        ChannelOwnerId = channelOwnerId,
                        GrantedUserId = authorizedUser.Id,
                        AccessLevel = dto.PermissionLevel,
                        GrantedBy = userId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _dbContext.UserChannelPermissions.Add(newPermission);
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Successfully saved permission changes");

                return Ok(new
                {
                    success = true,
                    message = "Usuario agregado correctamente",
                    user = new
                    {
                        id = authorizedUser.Id,
                        login = authorizedUser.Login,
                        displayName = authorizedUser.DisplayName,
                        accessLevel = dto.PermissionLevel
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in AddUserAccess: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error interno: {ex.Message}"
                });
            }
        }

        [HttpDelete("remove-access/{accessId}")]
        public async Task<IActionResult> RemoveUserAccess(long accessId)
        {
            try
            {
                var userId = GetUserId();
                var channelOwnerId = GetChannelOwnerId();

                // Solo el propietario puede remover usuarios
                if (userId != channelOwnerId && !await _permissionService.HasPermissionLevelAsync(userId, channelOwnerId, "control_total"))
                {
                    return Forbid("Solo el propietario del canal puede remover usuarios");
                }

                var permission = await _dbContext.UserChannelPermissions
                    .FirstOrDefaultAsync(p => p.Id == accessId && p.ChannelOwnerId == channelOwnerId);

                if (permission == null)
                {
                    return NotFound(new { success = false, message = "Permiso no encontrado" });
                }

                // Marcar como inactivo en lugar de eliminar
                permission.IsActive = false;
                permission.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"User access removed: {accessId} by user {userId}");
                return Ok(new { success = true, message = "Usuario removido correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user access");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("bot/status")]
        public async Task<IActionResult> GetBotStatus()
        {
            try
            {
                var userId = GetUserId();
                var channelOwnerId = GetChannelOwnerId();
                var settings = await _settingsService.GetSettingsByUserIdAsync(channelOwnerId);
                var connectedChannels = _botService.GetConnectedChannels();

                // Obtener nivel de acceso del usuario actual
                var userAccessLevel = await _permissionService.GetUserAccessLevelAsync(userId, channelOwnerId);
                var canModifySettings = userId == channelOwnerId || await _permissionService.HasPermissionLevelAsync(userId, channelOwnerId, "control_total");

                return Ok(new
                {
                    success = true,
                    botConnected = _botService.IsConnected,
                    botEnabledForUser = settings.BotEnabled,
                    connectedChannels = connectedChannels.Count,
                    channels = connectedChannels,
                    userAccessLevel = userAccessLevel,
                    canModifySettings = canModifySettings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bot status");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Obtiene la lista de usuarios con acceso al canal - CORREGIDO
        /// </summary>
        [HttpGet("channel-users")]
        public async Task<IActionResult> GetChannelUsers()
        {
            try
            {
                var userId = GetUserId();
                var channelOwnerId = GetChannelOwnerId();

                // Solo el propietario o usuarios con control total pueden ver la lista
                if (userId != channelOwnerId && !await _permissionService.HasPermissionLevelAsync(userId, channelOwnerId, "control_total"))
                {
                    return Forbid("No tienes permisos para ver la gestión de usuarios");
                }

                var users = await _dbContext.UserChannelPermissions
                    .Include(p => p.GrantedUser)
                    .Include(p => p.GrantedByUser)
                    .Where(p => p.ChannelOwnerId == channelOwnerId && p.IsActive)
                    .Select(p => new
                    {
                        id = p.Id,
                        userId = p.GrantedUserId,
                        username = p.GrantedUser.Login,
                        displayName = p.GrantedUser.DisplayName,
                        accessLevel = p.AccessLevel,
                        grantedBy = p.GrantedByUser.Login,
                        createdAt = p.CreatedAt
                        // Removido permissionLabel para evitar el error de EF Core
                    })
                    .ToListAsync();

                // Aplicar el label después de obtener los datos de la base
                var usersWithLabels = users.Select(u => new
                {
                    u.id,
                    u.userId,
                    u.username,
                    u.displayName,
                    u.accessLevel,
                    u.grantedBy,
                    u.createdAt,
                    permissionLabel = GetPermissionLabel(u.accessLevel)
                }).ToList();

                return Ok(new
                {
                    success = true,
                    users = usersWithLabels
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel users");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        private static string GetPermissionLabel(string accessLevel)
        {
            return accessLevel switch
            {
                "commands" => "Solo Comandos",
                "moderation" => "Moderación",
                "control_total" => "Control Total",
                _ => "Desconocido"
            };
        }
    }

    // DTOs (sin cambios)
    public class SystemSettingsDto
    {
        public bool BotEnabled { get; set; }
    }

    public class AddUserAccessDto
    {
        public string AuthorizedUserId { get; set; } = "";
        public string PermissionLevel { get; set; } = "";
    }
}