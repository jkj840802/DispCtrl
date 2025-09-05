using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace DispCtrl
{
    public partial class OffSchedulePage : ContentPage
    {
        private TimeSpan _offTime;
        private TimeSpan _onTime;
        private DateTime _lastOffSent = DateTime.MinValue;
        private DateTime _lastOnSent = DateTime.MinValue;

        private bool _autoEnabled = true;

        public OffSchedulePage()
        {
            InitializeComponent();

            // ����� �ð� �ҷ�����
            LoadTimes();
            OffTimePicker.Time = _offTime;
            OnTimePicker.Time = _onTime;

            // 30�ʸ��� üũ
            Dispatcher.StartTimer(TimeSpan.FromSeconds(30), CheckSchedule);
        }

        void LoadTimes()
        {
            // Preferences���� hh:mm �������� �о����, ���� �� �⺻�� ���
            if (!TimeSpan.TryParse(Preferences.Get("DisplayOffTime", "22:30"), out _offTime))
                _offTime = new TimeSpan(22, 30, 0);

            if (!TimeSpan.TryParse(Preferences.Get("DisplayOnTime", "05:30"), out _onTime))
                _onTime = new TimeSpan(5, 30, 0);
        }

        async void OnSaveClicked(object sender, EventArgs e)
        {
            // UI���� ���õ� �ð� ����
            _offTime = OffTimePicker.Time;
            _onTime = OnTimePicker.Time;
            Preferences.Set("DisplayOffTime", _offTime.ToString(@"hh\:mm"));
            Preferences.Set("DisplayOnTime", _onTime.ToString(@"hh\:mm"));

            await DisplayAlert("����", $"Off {_offTime.ToString(@"hh\:mm")}, On {_onTime.ToString(@"hh\:mm")} ������", "Ȯ��");
        }

        bool CheckSchedule()
        {
            if (!_autoEnabled) return false;   // ���� ������ Ÿ�̸� �ߴ�

            var now = DateTime.Now;
            var cur = now.TimeOfDay;

            // Off Ÿ�̹�
            if (cur.Hours == _offTime.Hours && cur.Minutes == _offTime.Minutes && _lastOffSent.Date != now.Date)
            {
                _ = SendCommandAsync("210");

                Console.WriteLine($@"
                    [WF] !!Ÿ�̸�OFF �߻�!
                      ������ OffTime = {_offTime:hh\:mm}
                      ���� now       = {now:HH:mm:ss}
                      cur            = {cur:hh\:mm}");
                _lastOffSent = now;
            }

            // On Ÿ�̹�
            if (cur.Hours == _onTime.Hours && cur.Minutes == _onTime.Minutes && _lastOnSent.Date != now.Date)
            {
                _ = SendCommandAsync("211");

                Console.WriteLine($@"
                    [WF] !!Ÿ�̸�ON �߻�!
                      ������ OnTime = {_onTime:hh\:mm}
                      ���� now       = {now:HH:mm:ss}
                      cur            = {cur:hh\:mm}");

                _lastOnSent = now;
            }

            return true; // Ÿ�̸� ��� ����
        }

        async Task SendCommandAsync(string cmdCode)
        {
            string? response1 = await WiFiSender.SendAndReceiveAsync(0, cmdCode);
            string? response2 = await WiFiSender.SendAndReceiveAsync(1, cmdCode);

            // �� �� �����ؾ� OK
            bool ok = response1 != null && response2 != null;

            System.Diagnostics.Debug.WriteLine(
                $"AutoSchedule send {cmdCode}: {(ok ? "OK" : "FAIL")}");
        }

        private void OnAutoScheduleToggled(object sender, ToggledEventArgs e)
        {
            _autoEnabled = e.Value;                // ��� ���� ����
            OffTimePicker.IsEnabled = e.Value;     // ��Ŀ Ȱ��/��Ȱ��
            OnTimePicker.IsEnabled = e.Value;
            if (_autoEnabled)
                Dispatcher.StartTimer(TimeSpan.FromSeconds(30), CheckSchedule);
        }
    }
}