using System.ComponentModel;

namespace DispCtrl
{
    public partial class App : Application
    {
        private readonly Exception? _loadException;

        public App()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                _loadException = ex.InnerException ?? ex;
            }
        }

        // 루트 윈도우를 생성할 때 네비게이션 페이지로 MainPage를 감싸서 반환
        protected override Window CreateWindow(IActivationState? activationState)
        {
            Page root;
            if (_loadException != null)
            {
                // XAML 로드 실패 시, 에러 메시지 전용 페이지 반환
                root = new ContentPage
                {
                    BackgroundColor = Colors.White,
                    Content = new Label
                    {
                        Text = $"XAML Load Error:\n{_loadException.Message}",
                        TextColor = Colors.Red,
                        Margin = new Thickness(20),
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                };
            }
            else
            {
                // 정상 로드 시, 기존 네비게이션 스택으로 MainPage 사용
                root = new NavigationPage(new MainPage())
                { 
                    BarBackgroundColor = Color.FromArgb("#F5F5F5"),
                    BarTextColor       = Colors.Black      
                };
            }

            return new Window(root);
        }
    }
}