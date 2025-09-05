using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using System.Runtime.Versioning;
using Microsoft.Maui.LifecycleEvents;

//
// 2) Then your assembly-level attributes:
//
[assembly: SupportedOSPlatform("android21.0")]
[assembly: SupportedOSPlatform("ios13.0")]
[assembly: SupportedOSPlatform("maccatalyst15.0")]
[assembly: SupportedOSPlatform("windows10.0.17763.0")]

namespace DispCtrl
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()    // 여기서 App 클래스가 MainPage를 설정합니다
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
