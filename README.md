# Decatron 2.0

**Decatron** es un bot modular completo para Twitch diseñado para gestión profesional de canales múltiples. Esta es la versión 2.0 open source, siendo reconstruida desde cero como una versión libre del bot principal Decatron 1.0 que incluye funcionalidades avanzadas para streamers profesionales.

> **Nota**: Este proyecto está en desarrollo activo y se agregan nuevas funcionalidades constantemente. Actualmente incluye las funciones básicas de gestión de categorías y títulos, con más características siendo migradas desde Decatron 1.0.

## Características Actuales

- **Comandos de gestión**: `!game`/`!g` y `!title`/`!t` para cambiar categorías y títulos
- **Micro-comandos personalizados**: Crea atajos rápidos para tus categorías favoritas (ej: `!apex`, `!valorant`)
- **Sistema de permisos jerárquico**: Control granular con tres niveles (commands, moderation, control_total)
- **Multi-canal**: Maneja múltiples streamers simultáneamente
- **Interfaz web completa**: Panel de administración con autenticación OAuth
- **TwitchTokenManager incluido**: Herramienta de escritorio para generar tokens fácilmente
- **Arquitectura modular**: Servicios independientes con Entity Framework Core
- **Logging avanzado**: Sistema de logs detallado con Serilog

## Características Planificadas (Migración desde Decatron 1.0)

El proyecto principal incluye muchas más funcionalidades que se irán agregando:
- Comandos personalizados avanzados con scripting
- Sistema de alias y comandos dinámicos
- Timers y temporizadores automáticos
- Sistema de followers y gestión de seguidores
- Overlays personalizables
- Protección anti-spam avanzada
- Sistema de clips automático
- Comandos de interacción (`!hola`, `!followage`, `!watchtime`, etc.)
- Y muchas más funcionalidades...

## Requisitos del Sistema

- **.NET 8 SDK** o superior
- **MySQL 8.0+** (o MariaDB compatible)
- **Microsoft Visual Studio** (recomendado para desarrollo)
- **Node/npm** (opcional, solo para compilar assets front-end manualmente)

## Configuración Rápida

### 1. Preparación del Proyecto

```bash
git clone https://github.com/decagraff/Decatron.git
cd Decatron
```

### 2. Configuración de Twitch Developer Console

Antes de configurar el bot, necesitas crear una aplicación en Twitch:

1. Ve a [Twitch Developer Console](https://dev.twitch.tv/console)
2. Inicia sesión y crea una nueva aplicación
3. Configura las **Redirect URIs**:
   - Para desarrollo: `https://localhost:7282/api/auth/callback`
   - Para producción: `https://tudominio.com/api/auth/callback`
4. Guarda el **Client ID** y **Client Secret**

### 3. Generación de Tokens con TwitchTokenManager

El proyecto incluye `TwitchTokenManager.exe`, una herramienta de escritorio desarrollada en Python/PyQt6 para generar tokens fácilmente:

1. **Ejecuta** `TwitchTokenManager.exe`
2. **Configura** Client ID, Client Secret y Redirect URI
3. **Selecciona** el tipo de token:
   - **User Access Token**: Para acciones en nombre del streamer
   - **App Access Token**: Para llamadas API generales
   - **Ambos Tokens**: **Recomendado** para configuración completa
4. **Configuración de Scopes**: 
   - El TwitchTokenManager incluye todos los scopes disponibles
   - **Recomendación**: Seleccionar "Seleccionar Todos" para máxima funcionalidad
   - Los scopes se usan solo para el User Access Token
5. **Copia** los tokens generados para el siguiente paso

**Recomendación Importante**: Usar una cuenta de bot separada (diferente a tu cuenta principal de streaming) para mayor seguridad y organización.

**Ejemplo de salida del TwitchTokenManager:**
```
═══════════════════════════════════════════════════════
                    DATOS COMPLETOS
═══════════════════════════════════════════════════════

📺 INFORMACIÓN DE CANAL:
   Channel ID: 1270917540
   Username: decatrontest
   Display Name: decatrontest

🔑 USER ACCESS TOKEN:
   idsw43x1jggb9880mzl92gcjb0p7h7
   
🔐 APP ACCESS TOKEN:
   8j9maw5ybe1xl1dgz89j24zvcl6vh6

⏰ GENERADO: 2025-10-22 22:48:51
```

### 4. Configuración de Archivos

#### 4.1 Configuración Principal (`appsettings.json`)

Copia `appsettings.example.json` a `appsettings.json` y ajusta:

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Port=3306;Database=decatron;User=tu_usuario;Password=tu_password;Connection Timeout=60;Default Command Timeout=60;"
    },
    "TwitchSettings": {
        "RedirectUri": "https://localhost:7282/api/auth/callback",
        "EventSubWebhookUrl": "https://localhost:7282/api/eventsub/webhook",
        "EventSubWebhookPort": 7282,
        "Scopes": "chat:read chat:edit clips:edit channel:bot channel:edit:commercial channel:manage:broadcast channel:manage:redemptions channel:read:editors channel:read:redemptions channel:read:subscriptions channel:read:vips moderation:read moderator:read:followers user:read:email user:edit:broadcast channel_editor user:manage:blocked_users user:write:chat"
    }
}
```

#### 4.2 Configuración de Secretos (`appsettings.Secrets.json`)

Copia `appsettings.Secrets.example.json` a `appsettings.Secrets.json` y configura con los datos del TwitchTokenManager:

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Port=3306;Database=decatron;User=tu_usuario;Password=tu_password;Connection Timeout=60;Default Command Timeout=60;"
    },
    "TwitchSettings": {
        "ClientId": "tu_client_id_aqui",
        "ClientSecret": "tu_client_secret_aqui",
        "BotUsername": "tu_bot_username",
        "ChannelId": "tu_channel_id",
        "WebhookSecret": "tu_webhook_secret",
        "EventSubWebhookSecret": "tu_eventsub_webhook_secret"
    },
    "JwtSettings": {
        "SecretKey": "tu_jwt_secret_key_aqui",
        "ExpiryMinutes": 60,
        "RefreshTokenExpiryDays": 7
    }
}
```

### 5. Configuración de Base de Datos

#### 5.1 Crear Base de Datos

Crea la base de datos manualmente si tu usuario no tiene permisos para crearla automáticamente:

```sql
CREATE DATABASE decatron CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

#### 5.2 Aplicar Migraciones

```bash
# Generar la migración inicial (si no existe)
dotnet ef migrations add InitialCreate

# Aplicar migraciones a la base de datos
dotnet ef database update
```

#### 5.3 Configurar Tokens del Bot

Inserta los tokens en la tabla `bot_tokens`:

```sql
INSERT INTO bot_tokens (BotUsername, AccessToken, ChatToken, CreatedAt, UpdatedAt, IsActive)
VALUES ('tu_bot_username', 'APP_ACCESS_TOKEN_AQUI', 'USER_ACCESS_TOKEN_AQUI', NOW(), NOW(), 1);
```

**Importante:** 
- `AccessToken`: Usar el App Access Token
- `ChatToken`: Usar el User Access Token (puede incluir o no el prefijo `oauth:`)

### 6. Ejecutar la Aplicación

```bash
dotnet run
```

La aplicación estará disponible en:
- **Web Interface**: `https://localhost:7282`
- **Debug Endpoints**: 
  - `https://localhost:7282/debug/ping`
  - `https://localhost:7282/debug/controllers`

## Comandos Disponibles

### Comandos de Categoría

#### `!game` / `!g`
Gestiona la categoría/juego del stream.

**Uso básico:**
```
!game                          # Mostrar categoría actual
!game Apex Legends            # Cambiar a Apex Legends
!g                            # Mostrar categoría actual
!g Counter-Strike 2          # Cambiar categoría
```

**Funciones avanzadas del `!g`:**
```
!g help                       # Mostrar ayuda
!g list                       # Listar micro-comandos disponibles
!g set !apex Apex Legends     # Crear micro-comando !apex
!g remove !apex               # Eliminar micro-comando !apex
```

#### Micro-comandos
Una vez creados, puedes usar micro-comandos directamente:
```
!apex                         # Cambia automáticamente a Apex Legends
!valorant                     # Cambia a Valorant (si está configurado)
```

### Comandos de Título

#### `!title` / `!t`
Gestiona el título del stream.

**Uso:**
```
!title                        # Mostrar título actual
!title Mi nuevo título        # Cambiar título
!t                           # Mostrar título actual
!t Stream de clasificatorias # Cambiar título
```

## Sistema de Permisos

Decatron implementa un sistema de permisos jerárquico de tres niveles:

### Niveles de Acceso

1. **Commands (Nivel 1)**: Puede usar comandos básicos
   - Comandos: `!game`, `!title`, micro-comandos
   
2. **Moderation (Nivel 2)**: Incluye commands + moderación
   - Overlays, timers, loyalty, filtros de chat
   
3. **Control Total (Nivel 3)**: Acceso completo
   - Gestión de usuarios, configuración del sistema

### Gestión de Permisos

**Dueño del canal**: Tiene control total automáticamente

**Dar permisos a otros usuarios**:
1. El usuario debe autenticarse en el panel web
2. Obtener su ID único desde su perfil
3. El dueño del canal agrega permisos usando ese ID

## Deployment

### Desarrollo Local

1. Configura `RedirectUri` y `EventSubWebhookUrl` con `localhost:7282`
2. Usa el TwitchTokenManager con las mismas URLs
3. Ejecuta con `dotnet run`

### Producción

1. **Actualiza URLs en configuración**:
   ```json
   "RedirectUri": "https://tudominio.com/api/auth/callback",
   "EventSubWebhookUrl": "https://tudominio.com/api/eventsub/webhook"
   ```

2. **Regenera tokens** con TwitchTokenManager usando las URLs de producción

3. **Deploy opciones**:
   - VPS con nginx/Apache como proxy reverso
   - Docker (dockerfile incluido)
   - Cloud providers (Azure, AWS, etc.)

4. **Configuración de HTTPS**: Requerido para webhooks de Twitch

## Estructura del Proyecto

```
Decatron/
├── Decatron.Core/              # Modelos e interfaces principales
├── Decatron.Data/              # Entity Framework y repositorios
├── Decatron.Services/          # Lógica de negocio y servicios
├── Decatron.Default/           # Comandos predeterminados
├── Decatron.Controllers/       # API Controllers
├── Decatron.Middleware/        # Middleware personalizado
├── Pages/                      # Razor Pages (interfaz web)
├── wwwroot/                    # Assets estáticos
├── TwitchTokenManager.exe      # Herramienta de tokens
└── Migrations/                 # Migraciones de base de datos
```

## Logs y Debugging

Los logs se almacenan en `logs/decatron-YYYY-MM-DD.txt` con rotación diaria.

**Verificar estado del bot:**
```
[INFO] Decatron starting up...
[INFO] Twitch Bot Username: tu_bot_username
[INFO] Channel ID: tu_channel_id
[INFO] Application ready, listening on configured ports
```

## Desarrollo y Contribución

### Estado del Proyecto

Decatron 2.0 está siendo desarrollado como la versión open source de Decatron 1.0. El proyecto principal incluye muchas más funcionalidades que se irán migrando gradualmente:

- **Sistema de comandos personalizado completo** con scripting
- **Gestión avanzada de followers**
- **Overlays dinámicos**
- **Sistema de timers automáticos**
- **Protección anti-spam avanzada**
- **Y muchas funcionalidades más...**

### Agregar Nuevos Comandos

1. Crear clase que implemente `ICommand` en `Decatron.Services/Commands/`
2. Registrar en el `CommandService`
3. Aplicar migraciones si necesita cambios en BD

### Herramientas de Desarrollo

- **Visual Studio**: Recomendado para el desarrollo del proyecto
- **Entity Framework Core**: Para migraciones de base de datos
- **PyQt6**: Para el TwitchTokenManager (si necesitas modificarlo)

### Extensiones

- El sistema está diseñado para ser modular
- Servicios independientes permiten fácil extensión
- Sistema de permisos reutilizable para nuevas funcionalidades
- Arquitectura preparada para agregar más comandos y características

## Solución de Problemas

### Bot no se conecta
- Verifica que los tokens sean válidos y estén en `bot_tokens`
- Confirma que `BotUsername` coincida con el usuario de los tokens
- Revisa logs para errores específicos

### Comandos no funcionan
- Verifica que el comando esté habilitado en la configuración
- Confirma permisos del usuario
- Revisa que el bot esté activo para el canal

### Errores de base de datos
- Confirma que la cadena de conexión sea correcta
- Verifica que las migraciones estén aplicadas
- Asegúrate de que la base de datos exista

## Licencia

Este proyecto está bajo la licencia especificada en `LICENSE.txt`.

---

**README para facilitar el arranque del proyecto Decatron. Ajusta las instrucciones según tu entorno y credenciales específicas.**