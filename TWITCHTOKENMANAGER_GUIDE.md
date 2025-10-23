# Guía Completa del TwitchTokenManager

El **TwitchTokenManager** es una herramienta de escritorio desarrollada en Python/PyQt6 que simplifica enormemente la generación de tokens para Decatron. Esta guía explica su uso detallado y todas sus funcionalidades.

## Características del TwitchTokenManager

- **Interfaz gráfica intuitiva** con diseño profesional
- **Tres modos de generación** de tokens
- **Selección completa de scopes** con interfaz visual
- **Configuración automática** para Decatron
- **Copia automática** de configuraciones JSON
- **Validación automática** de tokens generados

## Requisitos

- **Windows** (ejecutable .exe incluido)
- **Conexión a internet** para comunicarse con Twitch API
- **Navegador web** para autorización OAuth

## Configuración Inicial

### 1. Datos Necesarios

Antes de usar TwitchTokenManager, necesitas tener:

- **Client ID** de tu aplicación en Twitch Developer Console
- **Client Secret** de tu aplicación
- **Redirect URI** configurada (ej: `https://localhost:7282/api/auth/callback`)

### 2. Primera Ejecución

1. Ejecuta `TwitchTokenManager.exe`
2. Se abrirá una ventana de configuración inicial
3. Ingresa los datos de tu aplicación de Twitch
4. Confirma la configuración

## Modos de Generación

### Modo 1: User Access Token

**Uso**: Para acciones que requieren permisos específicos del usuario (cambiar título, categoría, etc.)

#### Proceso:
1. Selecciona **"User Access Token"**
2. **Configura Scopes**:
   - Usa **"Seleccionar Todos"** para máxima compatibilidad
   - O selecciona scopes específicos según necesidades
3. Haz clic en **"Generar User Token"**
4. Se abrirá una ventana con:
   - **URL de autorización** (se copia automáticamente)
   - **Código de verificación** de 6-8 caracteres
5. **Autoriza en el navegador**:
   - Pega la URL en tu navegador
   - Inicia sesión con la cuenta del bot
   - Ingresa el código mostrado
   - Autoriza la aplicación
6. Vuelve al TwitchTokenManager y confirma
7. El token se genera automáticamente

#### Salida:
```
ACCESS TOKEN: idsw43x1jggb9880mzl92gcjb0p7h7
TOKEN TYPE: bearer
EXPIRES IN: 14664 segundos
CHANNEL ID: 1270917540
USERNAME: decatrontest
DISPLAY NAME: decatrontest
```

### Modo 2: App Access Token

**Uso**: Para llamadas generales a la API que no requieren permisos específicos del usuario.

#### Proceso:
1. Selecciona **"App Access Token"**
2. Haz clic en **"Generar App Token"**
3. Se genera automáticamente (no requiere autorización web)

#### Salida:
```
APP ACCESS TOKEN: 8j9maw5ybe1xl1dgz89j24zvcl6vh6
TOKEN TYPE: bearer
EXPIRES IN: 5489959 segundos
```

### Modo 3: Ambos Tokens (Recomendado)

**Uso**: Configuración completa para Decatron con todos los datos necesarios.

#### Proceso:
1. Selecciona **"Generar Ambos Tokens"**
2. **Configura Scopes** (recomendado: "Seleccionar Todos")
3. Haz clic en **"Generar AMBOS Tokens"**
4. Sigue el proceso de autorización OAuth
5. Se generan ambos tokens automáticamente

#### Salida Completa:
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

═══════════════════════════════════════════════════════
         CONFIGURACIÓN JSON PARA appsettings.Secrets.json
═══════════════════════════════════════════════════════

"TwitchSettings": {
    "ClientId": "tu_client_id",
    "ClientSecret": "tu_client_secret",
    "BotUsername": "decatrontest",
    "ChannelId": "1270917540",
    "WebhookSecret": "tu_webhook_secret",
    "EventSubWebhookSecret": "tu_webhook_secret"
},
"JwtSettings": {
    "SecretKey": "tu_jwt_secret",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
}
```

## Configuración de Scopes

### Interfaz de Scopes

El TwitchTokenManager presenta todos los scopes disponibles en una grilla organizada:

- **Vista en columnas**: Scopes organizados en 4 columnas para fácil lectura
- **Checkboxes individuales**: Cada scope se puede seleccionar independientemente
- **Scroll automático**: Si hay muchos scopes, la interfaz incluye scroll

### Botones de Control

- **"Seleccionar Todos"**: Marca todos los scopes disponibles
- **"Deseleccionar Todos"**: Desmarca todos los scopes

### Scopes Incluidos

El TwitchTokenManager incluye todos los scopes disponibles de Twitch API:

```
analytics:read:extensions    channel:manage:broadcast     moderator:read:followers
analytics:read:games        channel:read:charity         moderator:read:guest_star
bits:read                   channel:edit:commercial      moderator:manage:guest_star
channel:bot                 channel:read:editors         moderator:read:moderators
channel:manage:ads          channel:manage:extensions    moderator:read:shield_mode
channel:read:ads            channel:read:goals           moderator:manage:shield_mode
... y muchos más
```

### Recomendación de Scopes

**Para uso completo de Decatron**: Usar **"Seleccionar Todos"**

**Ventajas**:
- Compatibilidad total con funcionalidades actuales y futuras
- No necesidad de regenerar tokens al agregar características
- Simplifica la configuración

**Para uso específico**: Seleccionar solo los scopes necesarios para funcionalidades particulares

## Funcionalidades Avanzadas

### Copia Automática

- **"Copiar Token"**: Copia solo el token al portapapeles
- **"Copiar Todo"**: Copia toda la salida formateada
- **Copia selectiva**: Botones específicos para URL y códigos de verificación

### Validación Automática

- **Verificación de formato**: Valida que los tokens tengan el formato correcto
- **Verificación de conectividad**: Confirma que se puede conectar a Twitch API
- **Información de canal**: Obtiene automáticamente datos del canal asociado

### Gestión de Errores

El TwitchTokenManager maneja automáticamente:

- **Errores de red**: Reintentos automáticos en caso de problemas de conexión
- **Tokens expirados**: Avisos cuando los tokens están cerca de expirar
- **Errores de autorización**: Mensajes claros sobre problemas de OAuth
- **Configuración incorrecta**: Validación de Client ID y Secret

## Integración con Decatron

### Configuración Automática

El TwitchTokenManager genera automáticamente:

1. **Configuración para `appsettings.Secrets.json`**
2. **Configuración para `appsettings.json`**
3. **Datos de inserción SQL** para `bot_tokens`

### Flujo Completo

1. **Genera tokens** con TwitchTokenManager
2. **Copia la configuración JSON** generada
3. **Pega en archivos** de configuración de Decatron
4. **Ejecuta Decatron** - debería conectarse automáticamente

## Solución de Problemas

### Error: "Invalid Client"
- Verifica Client ID y Client Secret
- Confirma que la aplicación esté activa en Twitch Console

### Error: "Invalid Redirect URI"
- Asegúrate de que la URI coincida exactamente con Twitch Console
- Verifica el uso de HTTPS

### Error: "Authorization Pending"
- Completa la autorización en el navegador
- Verifica que hayas ingresado el código correcto

### Token no funciona en Decatron
- Confirma que usaste la cuenta correcta para generar tokens
- Verifica que el BotUsername coincida con el usuario que autorizó
- Revisa que los scopes incluyan los necesarios para las funcionalidades

## Seguridad

### Mejores Prácticas

- **No compartas** pantallas mientras generas tokens
- **Copia tokens inmediatamente** a archivos de configuración
- **Cierra TwitchTokenManager** después de usar
- **Usa cuenta de bot separada** de tu cuenta principal de streaming

### Gestión de Tokens

- **Regenera tokens** si sospechas compromiso
- **Rotación regular**: Considera regenerar tokens cada 30-60 días
- **Backup seguro**: Guarda tokens en ubicación segura para recovery

## Desarrollo del TwitchTokenManager

El TwitchTokenManager está desarrollado en:

- **Python 3.9+**
- **PyQt6** para la interfaz gráfica
- **Requests** para comunicación con Twitch API


---

**Guía completa del TwitchTokenManager para Decatron. Para soporte adicional, consulta la documentación principal del proyecto.**