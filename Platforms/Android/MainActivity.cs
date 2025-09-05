using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Locations;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace DispCtrl
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, 
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize 
                             | ConfigChanges.Orientation 
                             | ConfigChanges.UiMode 
                             | ConfigChanges.ScreenLayout 
                             | ConfigChanges.SmallestScreenSize 
                             | ConfigChanges.Density
                             | ConfigChanges.FontScale)]

    public class MainActivity : MauiAppCompatActivity
    {
        const int RequestLocationId = 0;

        readonly string[] LocationPermissions =
        {
            Manifest.Permission.AccessFineLocation,
            Manifest.Permission.AccessCoarseLocation
        };

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Android 10+ 이상 위치 권한 필요 (Wi-Fi SSID 접근 위해)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) != Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(this, LocationPermissions, RequestLocationId);
                }
            }
        }

        // ① AttachBaseContext 에서 새 ConfigurationContext 생성
        protected override void AttachBaseContext(Context? newBase)
        {
            // ② newBase가 null이면 기본 구현 호출
            if (newBase == null)
            {
                base.AttachBaseContext(null);
                return;
            }

            // ③ 리소스는 non-null임을 단언(!)하고 사용
            var config = newBase.Resources!.Configuration!;
            config.FontScale = 1.0f;

            // ④ 변경된 config로 새 컨텍스트 생성
            var ctx = newBase.CreateConfigurationContext(config)!;
            base.AttachBaseContext(ctx);
        }

        // ② 더 이상 UpdateConfiguration을 직접 호출할 필요가 없습니다
        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            // 이 메서드는 이제 config 변경 시 호출만 보장해 줄 뿐,
            // 폰트 스케일 적용은 AttachBaseContext 쪽에서 이미 처리됩니다.
        }

        protected override void OnPause()
        {
            base.OnPause();
            Console.WriteLine("[LIFECYCLE] OnPause - 백그라운드 진입 또는 화면 꺼짐");

            // 연결 해제 및 UI 갱신
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var app = Microsoft.Maui.Controls.Application.Current;
                var window = app?.Windows.Count > 0 ? app.Windows[0] : null;
                var page = window?.Page;

                Console.WriteLine("[WF] [LIFECYCLE] OnPause - 백그라운드 진입 또는 화면 꺼짐");

                if (page is MainPage mainPage)
                {
                    WiFiSender.Disconnect();
                    mainPage.UpdateConnectionStatus(false);
                    Console.WriteLine($"[WF] [LIFECYCLE] MainPage OnPause 접근 여부: {(page is MainPage ? "성공" : "실패")}");
                }
            });
        }

        protected override void OnResume()
        {
            base.OnResume();
            Console.WriteLine("[WF] [LIFECYCLE] OnResume - 포그라운드 복귀");

            // UI 복귀 메시지 표시
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var app = Microsoft.Maui.Controls.Application.Current;
                var window = app?.Windows.Count > 0 ? app.Windows[0] : null;
                var page = window?.Page;
                Console.WriteLine("[WF] [LIFECYCLE] OnResume - 화면 복귀");

                if (page is MainPage mainPage)
                {
                    Console.WriteLine($"[WF] [LIFECYCLE] MainPage OnResume 접근 여부: {(page is MainPage ? "성공" : "실패")}");
                }
            });

            if (!IsLocationEnabled())
            {
                // GPS 꺼져 있으면 설정화면으로 유도
                StartActivity(new Intent(Settings.ActionLocationSourceSettings));

                // 안전하게 Toast 호출 (null 경고 제거)
                var context = Android.App.Application.Context;
                Toast.MakeText(context, "Wi-Fi 정보를 확인하려면 GPS(위치 서비스)를 켜주세요.", ToastLength.Long)?.Show();
            }
        }

        private bool IsLocationEnabled()
        {
            var locationManager = (LocationManager?)GetSystemService(LocationService);
            return locationManager?.IsProviderEnabled(LocationManager.GpsProvider) == true
                || locationManager?.IsProviderEnabled(LocationManager.NetworkProvider) == true;
        }
    }
}