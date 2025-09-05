using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace DispCtrl
{
    public partial class ConnectionSettingsPage : ContentPage
    {
        // ������
        readonly string[] _methods = new[] { "Wi-Fi" };

        public ConnectionSettingsPage()
        {
            InitializeComponent();

            CommPicker.ItemsSource = _methods;

            // ������ ����� �� �������� (�⺻ BLE)
            var saved = Preferences.Get("CommMethod", "BLE");
            CommPicker.SelectedIndex = Array.IndexOf(_methods, saved);
            if (CommPicker.SelectedIndex < 0) CommPicker.SelectedIndex = 0;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // ���õ� ��� ��� ����
            if (CommPicker.SelectedIndex >= 0)
            {
                Preferences.Set("CommMethod", _methods[CommPicker.SelectedIndex]);
            }
            // ���� ȭ������ ���ư���
            await Navigation.PopAsync();
        }
    }
}