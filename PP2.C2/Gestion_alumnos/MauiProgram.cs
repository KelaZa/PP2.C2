using Gestion_alumnos.Servicios;
using Microsoft.Extensions.Logging;
using Gestion_alumnos.ViewModels;

#if WINDOWS
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
#endif

namespace Gestion_alumnos
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

#if WINDOWS
            builder.ConfigureMauiHandlers(handlers =>
            {
                PickerHandler.Mapper.AppendToMapping(
                    nameof(IPicker.Title),
                    (handler, picker) => ConfigurarPickerWindows(handler, picker));

                PickerHandler.Mapper.AppendToMapping(
                    nameof(IPicker.TitleColor),
                    (handler, picker) => ConfigurarPickerWindows(handler, picker));
            });
#endif

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Servicio de datos compartidos
            builder.Services.AddSingleton<BaseDatosService>();
            builder.Services.AddSingleton<DatosService>();
            builder.Services.AddSingleton<RiesgoService>();
            builder.Services.AddSingleton<CargaDatosPruebaService>();

            // Pantallas de nuestro sistema
            builder.Services.AddTransient<InicioPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<CalendarioPage>();

            // Navegación principal
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddTransient<InicioViewModel>();
            builder.Services.AddTransient<AsistenciaPage>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<CalendarioViewModel>();
            builder.Services.AddTransient<AsistenciaViewModel>();
            builder.Services.AddTransient<EntregaPage>();
            builder.Services.AddTransient<EntregaViewModel>();
            builder.Services.AddTransient<PanelPage>();
            builder.Services.AddTransient<PanelViewModel>();
            builder.Services.AddTransient<AdministracionPage>();
            builder.Services.AddTransient<TrabajosPage>();
            builder.Services.AddTransient<ReporteRiesgoPage>();
            builder.Services.AddTransient<ReporteRiesgoViewModel>();
            return builder.Build();
        }

#if WINDOWS
        private static void ConfigurarPickerWindows(IPickerHandler handler, IPicker picker)
        {
            var comboBox = handler.PlatformView;

            comboBox.Header = null;
            comboBox.HeaderTemplate = null;
            comboBox.PlaceholderText = picker.Title ?? string.Empty;
            comboBox.PlaceholderForeground = picker.TitleColor?.ToPlatform();
        }
#endif
    }
}
