using DispCtrl.Services;
using Microsoft.Maui.Controls;

namespace DispCtrl
{
    public partial class DisplaySettingsPage : ContentPage
    {
        private bool _isDisplayOn = true;

        string method = string.Empty;

        // 두 번째 줄은 시간이라 메서드로 매번 생성
        public const string DayTimePacketData =
            "0/P0000/F0203/X0072/Y0004/S0099" +
            "/i02월" +    // 월
            "/i03일" +    // 일
            "[/i26]" + // 요일
            "/i20:" +     // 시
            "/i22:" +     // 분
            "/i23";       // 초

        public const string DayPacketData =
            "/i02월" +    // 월
            "/i03일" +    // 일
            "[/i26]";     // 요일

        public const string TimePacketData =
            "/i20:" +     // 시
            "/i22:" +     // 분
            "/i23";       // 초

        // 0→5%, 1→25%, 2→50%, 3→75%, 4→99%
        private static readonly int[] BrightnessLevels = { 10, 25, 50, 75, 90 };

        public static readonly string _Title =
            "/C7 수온/C2(/U164) " +
            "/C7탁도/C2(/U208) " +
            "/C7온도/C2(/U164) " +
            "/C7습도/C2(%)";

        public static string BuildMainMessage3(string apResp)
        {
            if (string.IsNullOrWhiteSpace(apResp))
                throw new ArgumentNullException(nameof(apResp));

            // '[!00D1 26.1 56.1!]' -> '00D1 26.1 56.1'
            var trimmed = apResp.Trim('[', ']', '!');
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Console.WriteLine($"[WF] parts1: {parts[1]}, parts2: {parts[2]}");

            if (parts.Length < 3)
                throw new FormatException($"응답 형식이 올바르지 않습니다: {apResp}");

            // parts[1]은 온도, parts[2]는 습도
            var temperature = parts[1];
            var humidity = parts[2];

            // 최종 문자열 생성
            return
                "/C6   " + temperature + "  " +
                "/C1   " + humidity + "  ";
        }

        public static async Task<string> SenserData(string apResp)
        {
            const string defaultCmd = "/C6   00.0  /C1   00.0";
            string trimmed;
            string[] parts;

            // 내부 헬퍼: 배열 검증 후 명령 문자열 반환 여부
            bool TryBuild(string resp, out string cmd)
            {
                trimmed = resp.Trim('[', ']', '!');
                parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && double.TryParse(parts[1], out var t)
                    && double.TryParse(parts[2], out var h))
                {
                    cmd = $"/C6   {t:F1}  /C1   {h:F1}";
                    return true;
                }
                cmd = default!;
                return false;
            }

            // 1) 첫 파싱 시도
            if (TryBuild(apResp ?? string.Empty, out var result))
                return result;

            // 2) 재시도: 1초 대기 후 다시 호출
            await Task.Delay(1000);
            string retryResp = await WiFiSender.SendAndReceiveAsync(0, "D10") ?? string.Empty;
            if (TryBuild(retryResp, out result))
                return result;

            // 3) 두 번 모두 실패 시 → 디폴트, 안내문구
            // 현재 화면이 DisplaySettingsPage 라는 가정하에 MainPage 로부터 꺼내옵니다.
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window?.Page is MainPage mainPage)
            {
                // x:Name="StatusLabel" 으로 선언된 Label 찾아서 텍스트 업데이트
                var statusLabel = mainPage.FindByName<Label>("StatusLabel");
                if (statusLabel != null)
                    statusLabel.Text = "비정상적인 데이터가 수신되었습니다.\n잠시 후 다시 시도해주세요.";
            }
            return defaultCmd;
        }

        public static readonly string MainMessage4 = "/C5하단메세지입니다즐겁게테스트";

        public DisplaySettingsPage()
        {
            InitializeComponent();
            method = Preferences.Get("CommMethod", "BLE");
        }

        private void OnBrightnessSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            var slider = (Slider)sender;
            slider.Value = Math.Round(e.NewValue);
        }

        // 밝기 조절 버튼
        private async void OnSetBrightnessClicked(object sender, EventArgs e)
        {
            // 슬라이더 인덱스 → 0~4
            int idx = (int)BrightnessSlider.Value;
            // 인덱스에 대응하는 퍼센트
            int percent = BrightnessLevels[idx];
            // Dabit 프로토콜: "50" + 두 자리 숫자
            // 예: percent=5  → "5005"
            //     percent=25 → "5025"
            string cmd = $"50{percent:D2}";

            bool ok;
            var response1 = await WiFiSender.SendAndReceiveAsync(0, cmd);
            var response2 = await WiFiSender.SendAndReceiveAsync(1, cmd);
            ok = response1 != null && response2 != null;

            // 3) 결과 알림
            await DisplayAlert(
                "밝기 설정",
                ok ? "✅ 전송 성공" : "❌ 전송 실패",
                "확인");
        }

        // 시간 동기화
        private async void OnSyncTimeClicked(object sender, EventArgs e)
        {
            string pkt = "30" + MakeControllerTimePacket();

            string? response = await WiFiSender.SendAndReceiveAsync(0, "30" + MakeControllerTimePacket());
            bool ok = response != null;
            await DisplayAlert("리셋", ok ? "성공" : "실패", "확인");
        }

        // 리셋 버튼
        private async void OnResetDisplayClicked(object sender, EventArgs e)
        {
            bool ok;
            string Fomet_500= "4002180";
            string Fomet_600 = "4003180";

            var response1 = await WiFiSender.SendAndReceiveAsync(0, Fomet_500);
            var response2 = await WiFiSender.SendAndReceiveAsync(1, Fomet_600);
            ok = response1 != null && response2 != null;
            await DisplayAlert("리셋", ok ? "성공" : "실패", "확인");
        }


        /// <summary>
            /// 컨트롤러용 시간 패킷을 생성합니다.
            /// 예: "25"(년도) + "01"(월) + "01"(일) + "3"(화요일) + "12"(시) + "34"(분) + "56"(초)
        /// => "2501013123456"
            /// 요일 매핑: 1=일, 2=월, 3=화, 4=수, 5=목, 6=금, 7=토
        /// </summary>
        public static string MakeControllerTimePacket()
        {
            var now = DateTime.Now;

            int w = (int)now.DayOfWeek;

            // $"{yy}{MM}{dd}{W}{HH}{mm}{ss}" 조합
            return $"{now:yy}{now:MM}{now:dd}{w}{now:HH}{now:mm}{now:ss}";
        }

        private async void OnDisplayOnOffClicked(object sender, EventArgs e)
        {
            // 1) 패킷 조립
            //    _isDisplayOn == true  → 지금은 켜져 있으니 “끄기” 명령 코드 210
            //    _isDisplayOn == false → 지금은 꺼져 있으니 “켜기” 명령 코드 211
            string cmdCode = _isDisplayOn ? "210" : "211";

            // 2) 토글 플래그 뒤집기 & 버튼 텍스트 갱신
            //    클릭할 때마다 _isDisplayOn 값을 반전시켜서
            //    버튼에 “전광판 끄기” / “전광판 켜기” 문구를 표시
            _isDisplayOn = !_isDisplayOn;
            DisplayToggleButton.Text = _isDisplayOn
                ? "전광판 끄기"
                : "전광판 켜기";

            //    Wi-Fi 채널 0과 채널 1에 각각 같은 cmdCode를 보내고
            //    null이 아닌 응답이 돌아왔는지 확인
            string? response1 = await WiFiSender.SendAndReceiveAsync(0, cmdCode);
            string? response2 = await WiFiSender.SendAndReceiveAsync(1, cmdCode);

            //    두 응답이 모두 null이 아니면 성공으로 간주
            bool ok = response1 != null && response2 != null;

            // 4) 결과 알림
            //    _isDisplayOn 값에 따라 제목(“켜기”/“끄기”)을 정하고,
            //    ok 여부에 따라 “성공” 또는 “실패” 메시지를 팝업으로 띄웁니다.
            await DisplayAlert(
                _isDisplayOn ? "켜기" : "끄기",
                ok ? "✅ 전송 성공" : "❌ 전송 실패",
                "확인");
        }
    }
}
