using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Turnero.App.Auditoria;
using Turnero.App.Catalogos;
using Turnero.App.Seguridad;
using Turnero.App.Turnos;
using Turnero.App.Veterinarios;

namespace Turnero.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);
        builder.Services.AddSingleton(_ => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri("https://trabajo-final-vet-mov.runasp.net/"),
            Timeout = TimeSpan.FromSeconds(30)
        });
        builder.Services.AddSingleton<SesionService>();
        builder.Services.AddSingleton<AuthenticationStateProvider>(services =>
            services.GetRequiredService<SesionService>());
        builder.Services.AddSingleton<ApiGestion>();
        builder.Services.AddSingleton<VeterinariosApi>();
        builder.Services.AddSingleton<CatalogosApi>();
        builder.Services.AddSingleton<AuditoriaApi>();
        builder.Services.AddSingleton<TurnosApi>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
