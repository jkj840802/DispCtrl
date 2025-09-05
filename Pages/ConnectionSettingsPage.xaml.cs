using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace DispCtrl
{
    public partial class ConnectionSettingsPage : ContentPage
    {
        // 선택지
        readonly string[] _methods = new[] { "Wi-Fi" };

        public ConnectionSettingsPage()
        {
            InitializeComponent();

            CommPicker.ItemsSource = _methods;

            // 이전에 저장된 값 꺼내오기 (기본 BLE)
            var saved = Preferences.Get("CommMethod", "BLE");
            CommPicker.SelectedIndex = Array.IndexOf(_methods, saved);
            if (CommPicker.SelectedIndex < 0) CommPicker.SelectedIndex = 0;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // 선택된 통신 방식 저장
            if (CommPicker.SelectedIndex >= 0)
            {
                Preferences.Set("CommMethod", _methods[CommPicker.SelectedIndex]);
            }
            // 메인 화면으로 돌아가기
            await Navigation.PopAsync();
        }
    }
}