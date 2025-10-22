using System.Reflection;
using Decatron.Core.Interfaces;
using Decatron.Core.Settings;
using Decatron.Data;
using Decatron.Data.Repositories;
using Decatron.Middleware;
using Decatron.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using TwitchLib.Client;

var builder = WebApplication.CreateBuilder(args);

// Load user secrets in development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("=== DECATRON STARTUP DEBUG ===");
    Log.Information("Decatron starting up...");

    // Add services to the container
    builder.Services.AddRazorPages();

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(20);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    builder.Services.AddSignalR();
    builder.Services.AddHttpClient();

    Log.Information("Registrando Controllers...");
    builder.Services.AddControllers();

    // Bind configuration sections to classes
    builder.Services.Configure<TwitchSettings>(
        builder.Configuration.GetSection("TwitchSettings"));

    builder.Services.Configure<JwtSettings>(
        builder.Configuration.GetSection("JwtSettings"));

    // Add DbContext
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<DecatronDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

    // Add Authentication
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
        });

    // Add Authorization
    builder.Services.AddAuthorization();

    // Register repositories
    Log.Information("Registrando Repositories...");
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Register core services
    Log.Information("Registrando Core Services...");
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();

    builder.Services.AddSingleton<ICommandStateService, CommandStateService>();


    // Register EventSub service
    Log.Information("Registrando EventSub Service...");
    try
    {
        //builder.Services.AddScoped<IEventSubService, EventSubService>();
        Log.Information("IEventSubService registrado correctamente");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "ERROR registrando IEventSubService");
    }

    // Register Twitch services
    Log.Information("Registrando Twitch Services...");
    builder.Services.AddSingleton<TwitchClient>(provider =>
    {
        var client = new TwitchClient();
        return client;
    });
    builder.Services.AddSingleton<IMessageSender, MessageSenderService>();
    builder.Services.AddSingleton<TwitchApiService>();
    builder.Services.AddSingleton<TwitchBotService>();

    builder.Services.AddSingleton<Lazy<TwitchBotService>>(provider =>
    new Lazy<TwitchBotService>(() => provider.GetRequiredService<TwitchBotService>()));

    builder.Services.AddSingleton<CommandService>();


    Log.Information("Construyendo aplicación...");
    var app = builder.Build();

    Log.Information("=== CONFIGURANDO PIPELINE ===");

    // Configure the HTTP request pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    Log.Information("Configurando Routing...");
    app.UseRouting();

    app.UseSession();
    app.UseAuthentication();
    app.UseChannelAccess();
    app.UseAuthorization();

    Log.Information("Mapeando Razor Pages...");
    app.MapRazorPages();

    Log.Information("Mapeando Controllers...");
    app.MapControllers();

    // DEBUG: Endpoint para verificar que la app responde
    app.MapGet("/debug/ping", () =>
    {
        Log.Information("Ping recibido en /debug/ping");
        return Results.Ok(new
        {
            status = "OK",
            timestamp = DateTime.Now,
            message = "Aplicación funcionando correctamente"
        });
    });

    // DEBUG: Endpoint para mostrar controllers registrados
    app.MapGet("/debug/controllers", (IServiceProvider services) =>
    {
        Log.Information("Solicitando información de controllers...");

        try
        {
            // Buscar todos los tipos que heredan de ControllerBase
            var controllers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.ControllerBase)))
                .Select(t => new {
                    Name = t.Name,
                    Namespace = t.Namespace,
                    FullName = t.FullName
                })
                .ToList();

            Log.Information($"Controllers encontrados: {controllers.Count}");
            foreach (var ctrl in controllers)
            {
                Log.Information($"  - {ctrl.FullName}");
            }

            return Results.Ok(new
            {
                controllers_count = controllers.Count,
                controllers = controllers
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error obteniendo controllers");
            return Results.Problem("Error obteniendo controllers");
        }
    });

    Log.Information("Application configured, starting services...");

    try
    {
        var twitchBotService = app.Services.GetRequiredService<TwitchBotService>();

        // Log configuration loaded
        var twitchSettings = app.Services.GetRequiredService<IOptions<TwitchSettings>>().Value;
        Log.Information("=== Configuration Loaded ===");
        Log.Information($"Twitch Bot Username: {twitchSettings.BotUsername}");
        Log.Information($"Twitch Client ID: {twitchSettings.ClientId}");
        Log.Information($"Channel ID: {twitchSettings.ChannelId}");
        Log.Information($"EventSub Webhook URL: {twitchSettings.EventSubWebhookUrl}");
        Log.Information("===========================");

        // Initialize TwitchBotService
        _ = Task.Run(async () =>
        {
            try
            {
                await twitchBotService.Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error inicializando TwitchBotService");
            }
        });

        Log.Information("=== APLICACIÓN LISTA ===");
        Log.Information("Application ready, listening on configured ports");
        Log.Information("EventSub configurado para webhooks en: /api/eventsub/webhook");
        Log.Information("Endpoints de debug disponibles:");
        Log.Information("  - GET /debug/ping");
        Log.Information("  - GET /debug/controllers");
        Log.Information("  - GET /api/eventsub/test (si TestEventSubController está funcionando)");
        Log.Information("========================");
    }
    catch (Exception serviceEx)
    {
        Log.Error(serviceEx, "Error starting services");
    }

    // Protección: evita que EF Core ejecute el host cuando corre migraciones
    if (!AppDomain.CurrentDomain.FriendlyName.Contains("ef"))
    {
        var twitchBotService = app.Services.GetRequiredService<TwitchBotService>();
        _ = Task.Run(async () =>
        {
            try
            {
                await twitchBotService.Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error inicializando TwitchBotService");
            }
        });
    }


    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}