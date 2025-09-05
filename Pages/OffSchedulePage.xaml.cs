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

            // 저장된 시간 불러오기
            LoadTimes();
            OffTimePicker.Time = _offTime;
            OnTimePicker.Time = _onTime;

            // 30초마다 체크
            Dispatcher.StartTimer(TimeSpan.FromSeconds(30), CheckSchedule);
        }

        void LoadTimes()
        {
            // Preferences에서 hh:mm 포맷으로 읽어오기, 실패 시 기본값 사용
            if (!TimeSpan.TryParse(Preferences.Get("DisplayOffTime", "22:30"), out _offTime))
                _offTime = new TimeSpan(22, 30, 0);

            if (!TimeSpan.TryParse(Preferences.Get("DisplayOnTime", "05:30"), out _onTime))
                _onTime = new TimeSpan(5, 30, 0);
        }

        async void OnSaveClicked(object sender, EventArgs e)
        {
            // UI에서 선택된 시간 저장
            _offTime = OffTimePicker.Time;
            _onTime = OnTimePicker.Time;
            Preferences.Set("DisplayOffTime", _offTime.ToString(@"hh\:mm"));
            Preferences.Set("DisplayOnTime", _onTime.ToString(@"hh\:mm"));

            await DisplayAlert("저장", $"Off {_offTime.ToString(@"hh\:mm")}, On {_onTime.ToString(@"hh\:mm")} 설정됨", "확인");
        }

        bool CheckSchedule()
        {
            if (!_autoEnabled) return false;   // 꺼져 있으면 타이머 중단

            var now = DateTime.Now;
            var cur = now.TimeOfDay;

            // Off 타이밍
            if (cur.Hours == _offTime.Hours && cur.Minutes == _offTime.Minutes && _lastOffSent.Date != now.Date)
            {
                _ = SendCommandAsync("210");

                Console.WriteLine($@"
                    [WF] !!타이머OFF 발생!
                      설정된 OffTime = {_offTime:hh\:mm}
                      현재 now       = {now:HH:mm:ss}
                      cur            = {cur:hh\:mm}");
                _lastOffSent = now;
            }

            // On 타이밍
            if (cur.Hours == _onTime.Hours && cur.Minutes == _onTime.Minutes && _lastOnSent.Date != now.Date)
            {
                _ = SendCommandAsync("211");

                Console.WriteLine($@"
                    [WF] !!타이머ON 발생!
                      설정된 OnTime = {_onTime:hh\:mm}
                      현재 now       = {now:HH:mm:ss}
                      cur            = {cur:hh\:mm}");

                _lastOnSent = now;
            }

            return true; // 타이머 계속 유지
        }

        async Task SendCommandAsync(string cmdCode)
        {
            string? response1 = await WiFiSender.SendAndReceiveAsync(0, cmdCode);
            string? response2 = await WiFiSender.SendAndReceiveAsync(1, cmdCode);

            // 둘 다 성공해야 OK
            bool ok = response1 != null && response2 != null;

            System.Diagnostics.Debug.WriteLine(
                $"AutoSchedule send {cmdCode}: {(ok ? "OK" : "FAIL")}");
        }

        private void OnAutoScheduleToggled(object sender, ToggledEventArgs e)
        {
            _autoEnabled = e.Value;                // 토글 상태 저장
            OffTimePicker.IsEnabled = e.Value;     // 피커 활성/비활성
            OnTimePicker.IsEnabled = e.Value;
            if (_autoEnabled)
                Dispatcher.StartTimer(TimeSpan.FromSeconds(30), CheckSchedule);
        }
    }
}