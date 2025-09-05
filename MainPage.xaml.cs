using DispCtrl.Services;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DispCtrl
{
    public partial class MainPage : ContentPage
    {
        private string method = "Wi-Fi";
        private bool _manualVisible = false;
        private static readonly Regex _twoIntOneDecimal = new Regex(@"^\d{0,2}(\.\d?)?$", RegexOptions.Compiled);
        private static readonly Regex _oneIntTwoDecimal = new(@"^([0-9])(\.\d{0,2})?$", RegexOptions.Compiled);

        // ✅ 연결 상태 ViewModel 속성
        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged(nameof(IsConnected));  // ContentPage가 이미 제공함
                }
            }
        }

        public MainPage()
        {
            InitializeComponent();

            // 바인딩 컨텍스트 설정
            BindingContext = this;

            // 통신 방식 표시
            CommStatusLabel.Text = method;

            // 네트워크 변화 이벤트 구독
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

            // 토글 UI 초기화
            UpdateManualToggleUI();

            // 1시간마다 자동 전송
            Dispatcher.StartTimer(TimeSpan.FromHours(1), () =>
            {
                _ = SendHourlyDataAsync();
                return true;
            });
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            Console.WriteLine($"[WF] OnConnectivityChanged 호출됨: NetworkAccess={e.NetworkAccess}, Profiles={string.Join(",", e.ConnectionProfiles)}");

            bool osWifiUp = e.NetworkAccess == NetworkAccess.Internet
                            && e.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
            bool onDispAp = IsOnDispNetwork();

            Dispatcher.Dispatch(() =>
            {
                if (osWifiUp && onDispAp)
                {
                    ConnectionStatusLabel.Text = "✅ AP-disp1234 네트워크 연결됨";
                    ConnectionStatusLabel.TextColor = Colors.Green;

                    CommStatusLabel.Text = "Wi-Fi";
                    CommStatusLabel.TextColor = Colors.Green;
                }
                else if (osWifiUp)
                {
                    ConnectionStatusLabel.Text = "❌ 다른 Wi-Fi 네트워크에 연결됨";
                    ConnectionStatusLabel.TextColor = Colors.Orange;
                }
                else
                {
                    ConnectionStatusLabel.Text = "❌ Wi-Fi 연결 안됨";
                    ConnectionStatusLabel.TextColor = Colors.Red;
                }
            });
        }

        /// <summary>
        /// 192.168.4.x 대역 AP-disp1234 에 연결되어 있는지 검사
        /// </summary>
        private bool IsOnDispNetwork()
        {
            var ipv4Addrs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(i => i.OperationalStatus == OperationalStatus.Up
                         && i.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(u => u.Address.ToString())
                .ToList();

            Console.WriteLine($"[WF] 사용 중인 IPv4 주소들: {string.Join(", ", ipv4Addrs)}");

            bool onDisp = ipv4Addrs.Any(a => a.StartsWith("192.168.4."));
            Console.WriteLine($"[WF] IsOnDispNetwork 결과: {onDisp}");

            return onDisp;
        }

        private async void OnConnectButtonClicked(object sender, EventArgs e)
        {
            ConnectButton.IsEnabled = false;

            try
            {
                if (IsConnected)
                {
                    await WiFiSender.DisconnectAsync();
                    IsConnected = false;
                    ConnectionStatusLabel.Text = "❌ 연결 해제됨";
                    ConnectionStatusLabel.TextColor = Colors.Red;
                    CommStatusLabel.Text = "Wi-Fi";
                    CommStatusLabel.TextColor = Colors.Red;
                }
                else
                {
                    ConnectionStatusLabel.Text = "연결 시도 중…";
                    ConnectionStatusLabel.TextColor = Colors.White;

                    if (!IsOnDispNetwork())
                    {
                        ConnectionStatusLabel.Text = "❌ AP-disp1234 네트워크에 연결해 주세요";
                        ConnectionStatusLabel.TextColor = Colors.Red;
                        CommStatusLabel.Text = "Wi-Fi(X)";
                        CommStatusLabel.TextColor = Colors.Red;
                        return;
                    }

                    await Task.Delay(2000);  // or 1500ms도 테스트 가능

                    bool wifiOk = await WiFiSender.ConnectAllAsync();

                    Console.WriteLine($"[WF] 🔄[Connect 예외] {wifiOk}");

                    if (!wifiOk)
                    {
                        ConnectionStatusLabel.Text = "❌ 와이파이 예외 발생";
                        ConnectionStatusLabel.TextColor = Colors.Red;
                        CommStatusLabel.Text = "Wi-Fi(X)";
                        CommStatusLabel.TextColor = Colors.Red;
                    }
                    else
                    {
                        await SendDb500SequenceAsync(1);
                        IsConnected = true;
                        ConnectionStatusLabel.Text = "✅ 컨트롤러 연결 완료";
                        ConnectionStatusLabel.TextColor = Colors.Green;
                        CommStatusLabel.Text = "Wi-Fi(OK)";
                        CommStatusLabel.TextColor = Colors.Green;

                        WiFiSender.StartConnectionMonitor(() =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                UpdateConnectionStatus(false); // 연결 끊겼을 때 UI 반영
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusLabel.Text = "❌ 연결 예외 발생";
                ConnectionStatusLabel.TextColor = Colors.Red;
                Console.WriteLine($"[WF] 🔄[Connect 예외] {ex}");
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        public void UpdateConnectionStatus(bool isConnected)
        {
            IsConnected = isConnected;

            if (isConnected)
            {
                ConnectionStatusLabel.Text = "✅ 연결됨";
                ConnectionStatusLabel.TextColor = Colors.Green;
                CommStatusLabel.Text = "Wi-Fi(OK)";
                CommStatusLabel.TextColor = Colors.Green;
            }
            else
            {
                ConnectionStatusLabel.Text = "❌ 연결 해제됨";
                ConnectionStatusLabel.TextColor = Colors.Red;
                CommStatusLabel.Text = "Wi-Fi(X)";
                CommStatusLabel.TextColor = Colors.Red;
            }
        }

        /// <summary>
        /// “🔌 메인 환경설정” 버튼 클릭 핸들러
        /// </summary>
        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            // ActionSheet 로 메뉴 표시
            string action = await DisplayActionSheet(
                "환경설정",     // 제목
                "취소",         // 취소 버튼 텍스트
                null,           // 파괴적 버튼 텍스트 (없음)
                "통신설정",     // 첫 번째 항목
                "화면 ON_OFF 설정",
                "전광판설정"
            );

            if (action == "통신설정")
            {
                // 기존 통신설정 페이지로 이동
                await Navigation.PushAsync(new ConnectionSettingsPage());
            }
            else if (action == "전광판설정")
            {
                // 새로 만든 전광판설정 페이지로 이동
                await Navigation.PushAsync(new DisplaySettingsPage());
            }
            else if (action == "화면 ON_OFF 설정")
            {
                // 새로 만든 전광판설정 페이지로 이동
                await Navigation.PushAsync(new OffSchedulePage());
            }
            // 취소 또는 그 외는 아무 동작 없음
        }

        private async Task SendHourlyDataAsync()
        {
            // === 연결 가드 추가 ===
            if (!WiFiSender.IsConnected)
            {
                Console.WriteLine("[HourlySend] 연결 없음 → 이번 사이클 스킵 & 재연결 시도");
                _ = WiFiSender.TryReconnectAsync(); // 비동기 백오프 재연결
                return; // 바로 종료
            }

            bool ok = true;

            // 1) 센서값 읽기
            var sht31 = await WiFiSender.SendAndReceiveAsync(0, "D10");
            string sht31Value = await DisplaySettingsPage.SenserData(sht31!);

            // 2) 저장된 수온·탁도 불러오기
            string savedTemp = Preferences.Default.Get("LastWaterTemp", "");
            string savedTurb = Preferences.Default.Get("LastTurbidity", "");

            // 3) 페이로드 조합
            string payload = $"1/P0002/F0203/X0072/Y0812/S0099"
                           + $"/C6{savedTemp.PadRight(4)}"
                           + $"/C1{savedTurb.PadRight(4)}"
                           + sht31Value;

            var resp = await WiFiSender.SendAndReceiveAsync(1, payload);
            ok &= resp != null;
            await Task.Delay(100);


            // (선택) 디버그 로그
            Console.WriteLine($"[HourlySend] {payload} → {(ok ? "OK" : "FAIL")}");
        }

        private async void OnUrgentMessageClicked(object sender, EventArgs e)
        {
            try
            {
                // 네비게이션 시 예외를 잡아냅니다
                await Navigation.PushAsync(new UrgentMessagePage());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WF] 긴급메시지 페이지를 불러올 수 없습니다 : {ex.Message}");
                // 사용자에게 에러 메시지로 알리면 앱이 꺼지지 않습니다
                await DisplayAlert("오류", $"긴급메시지 페이지를 불러올 수 없습니다:\n{ex.Message}", "확인");
            }
        }

        private async void OnSendButtonClicked(object sender, EventArgs e)
        {
            if (!_isConnected)
                return;

            try
            {
                bool IsValidSensorResponse(string resp)
                {
                    var trimmed = resp.Trim('[', ']', '!');
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 3
                        && double.TryParse(parts[1], out _)
                        && double.TryParse(parts[2], out _);
                }

                // 실제 호출부
                string raw = string.Empty;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    raw = await WiFiSender.SendAndReceiveAsync(0, "D10") ?? string.Empty;
                    Console.WriteLine($"[WF] 시도{attempt + 1} SHT31 응답: {raw}");
                    if (IsValidSensorResponse(raw))
                        break;                    // 유효하면 즉시 탈출
                    await Task.Delay(1000);      // 1초 대기 후 재시도
                }

                // 두 번 모두 실패했으면 기본값 사용
                if (!IsValidSensorResponse(raw))
                {
                    Console.WriteLine("[WF] ⚠️ 유효 응답 아님, 기본값 사용");
                    raw = "[!00D1 00.0 00.0!]";
                }

                // 최종 메시지 빌드
                string sht31Message = DisplaySettingsPage.BuildMainMessage3(raw);
                Console.WriteLine($"[WF] 최종 전송용 메시지: {sht31Message}");
                await Task.Delay(100);

                //DB500 메세지
                await SendDb500SequenceAsync(0);

                //DB600 메세지
                // 1) 실시간 동기화 + 응답
                // 2) 첫 번째 명령 (재시도)

                string cmd1 = $"1/P0000/F0203/X0072/Y0004/S0099{DisplaySettingsPage._Title}";
                while (true)
                {
                    var resp1 = await WiFiSender.SendAndReceiveAsync(1, cmd1);
                    Console.WriteLine($"[WF] 600-P0000 응답: {resp1}");
                    if (resp1 is "![0000!]" or "![0010!]")
                        break;               // 성공이면 다음으로
                    await Task.Delay(100);   // 실패·타임아웃 시 재시도
                }
                await Task.Delay(100);

                // 3) 두 번째 명령 (재시도)
                string cmd2 = $"1/P0001/F0203/X0072/Y0408/S0099/C6   00.0  /C1   00.0  {sht31Message}";
                while (!await WiFiSender.SendCommandToHostAsync(1, cmd2))
                {
                    await Task.Delay(100);
                }
                await Task.Delay(100);

                string cmd3 = "1/P0002/F0203/X0072/Y0812/S0099";
                string BMsg = Preferences.Default.Get("ButtonMsg", string.Empty);

                cmd3 = !string.IsNullOrEmpty(BMsg)
                    ? cmd3 + BMsg
                    : cmd3 + "/C1 ";

                while (true)
                {
                    var resp3 = await WiFiSender.SendAndReceiveAsync(1, cmd3);
                    Console.WriteLine($"[WF] 600-P0002 응답: {resp3}");
                    if (resp3 is "![0000!]" or "![0010!]")
                        break;
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                // 예외 발생 시
                Console.WriteLine($"[Send 예외] {ex}");
            }
            finally
            {
                StatusLabel.Text = "메세지 전송 완료";
            }
        }

        /// <summary>
        /// DB500 전광판으로 3단계 명령 전송 후, 시간 응답(LTime)을 반환합니다.
        /// </summary>
        /// <param name="realTime">채널 번호 (0 또는 1)</param>
        /// <returns>전송 후 수신된 시간 문자열 (없으면 빈 문자열)</returns>
        private async Task<string> SendDb500SequenceAsync(int realTime = 0)
        {
            // 1) 첫 번째 명령 //총 12글자 사이즈 유지해야 함
            string cmd1 = $"{realTime}/P0000/F0203/X0048/Y0008/C2유앤아이센터 수영장     /C3{DisplaySettingsPage.DayPacketData}";
            while (true)
            {
                var resp1 = await WiFiSender.SendAndReceiveAsync(0, cmd1);
                Console.WriteLine($"[WF] 500-P0000 응답: {resp1}");
                if (resp1 is "![0000!]" or "![0010!]")
                    break;               // 성공이면 다음으로
                await Task.Delay(1000);   // 실패·타임아웃 시 재시도
            }
            
            // 2) 세 번째 명령
            string cmd3 = $"{realTime}/P0001/F0205/X4872/Y0008/C3{DisplaySettingsPage.TimePacketData}";
            while (true)
            {
                var resp3 = await WiFiSender.SendAndReceiveAsync(0, cmd3);
                Console.WriteLine($"[WF] 500-P0002 응답: {resp3}");
                if (resp3 is "![0000!]" or "![0010!]")
                    break;
                await Task.Delay(1000);
            }
            
            // 3) 시간 요청 / 응답 받기 (원본 그대로)
            string requestPacket = "30" + DisplaySettingsPage.MakeControllerTimePacket();
            string lTime = await WiFiSender.SendAndReceiveAsync(0, requestPacket) ?? string.Empty;
            Console.WriteLine($"[WF] ❌ LTime (채널 0): {lTime}");
            await Task.Delay(100);

            return lTime;
        }


        // “전광판 제목 수정” 버튼 클릭 시 호출
        private async void OnEditTitleClicked(object sender, EventArgs e)
        {
            // TitleMessagePage 로 이동
            await Navigation.PushAsync(new TitleMessagePage());
        }

        private async void OnBottomMessageClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new BottomMessagePage());
        }

        /// <summary>
        /// “값 전송” 버튼 클릭 핸들러 (4번째 줄만 업데이트)
        /// </summary>
        private async void OnValueSendClicked(object sender, EventArgs e)
        {
            bool ok = true;

            if (!WiFiSender.IsConnected)
            {
                StatusLabel.Text = "컨트롤러 미연결!";

                StatusLabel.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.OrangeRed
                    : Colors.Red;
                return;
            }

            // 2) 페이로드 결정
            string payload;

            string Normalize(string? input)
            {
                if (double.TryParse(input, out var val))
                    // F1 대신 "00.0" 포맷으로: 정수부 두 자리+소수점 한 자리
                    return val.ToString("00.0", CultureInfo.InvariantCulture);
                // 파싱 실패 시에도 "00.0" 반환
                return (0.0).ToString("00.0", CultureInfo.InvariantCulture);
            }

            if (!_manualVisible)  // 자동모드
            {
                // --- 1) 센서 응답 유효성 검사 함수 (로컬 함수) ---
                bool IsValidSensor(string resp)
                {
                    var parts = resp
                        .Trim('[', ']', '!')
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 3
                        && double.TryParse(parts[1], out _)
                        && double.TryParse(parts[2], out _);
                }

                // --- 2) 최대 2회 재시도 로직 ---
                string raw = string.Empty;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    raw = await WiFiSender.SendAndReceiveAsync(0, "D10") ?? string.Empty;
                    Console.WriteLine($"[WF] 시도 {attempt + 1} SHT31 응답: {raw}");
                    if (IsValidSensor(raw))
                        break;                  // 유효하면 즉시 탈출
                    await Task.Delay(1000);    // 1초 대기 후 재시도
                }

                // --- 3) 두 번 모두 실패 시 기본값 할당 ---
                if (!IsValidSensor(raw))
                {
                    Console.WriteLine("[WF] ⚠️ 유효 응답 아님, 기본값 사용");
                    raw = "[!00D1 00.0 00.0!]";
                    StatusLabel.Text = "센서 비정상, 기본값 전송";  // (선택) UI 알림
                    StatusLabel.TextColor = Colors.Orange;
                }

                // --- 4) 메시지 생성 & 페이로드 조립 ---
                string SHT31Value = DisplaySettingsPage.BuildMainMessage3(raw);
                Console.WriteLine($"[WF] 최종 SHT31Value: {SHT31Value}");

                string w = Normalize(WaterTempEntry?.Text?.Trim());
                string t = Normalize(TurbidityEntry?.Text?.Trim());

                payload = $"1/P0001/F0203/X0072/Y0408/S0099" +
                          $"/C6   {w}  " +
                          $"/C1   {t}  " +
                          SHT31Value;
            }
            else  // 수동모드
            {
                string w = Normalize(WaterTempEntry?.Text?.Trim());
                string t = Normalize(TurbidityEntry?.Text?.Trim());
                string p = Normalize(IndoorTempEntry?.Text?.Trim());
                string c = Normalize(HumidityEntry?.Text?.Trim());

                payload = $"1/P0001/F0203/X0072/Y0408/S0099" +
                          $"/C6   {w}  " +
                          $"/C1   {t}  " +
                          $"/C6   {p}  " +
                          $"/C1   {c}";
            }

            var resp = await WiFiSender.SendAndReceiveAsync(1, payload);
            ok &= resp != null;
            await Task.Delay(100);

            // (2) Preferences에 저장
            Preferences.Default.Set("LastWaterTemp", WaterTempEntry?.Text);
            Preferences.Default.Set("LastTurbidity", TurbidityEntry?.Text);

            // 4) 상태 표시
            StatusLabel.Text = ok ? "✅ 전송 완료" : "❌ 전송 실패";
            StatusLabel.TextColor = ok ? Colors.Green : Colors.Red;
        }

        private void OnManualToggleClicked(object sender, EventArgs e)
        {
            // 토글 상태 반전
            _manualVisible = !_manualVisible;
            UpdateManualToggleUI();
        }

        private void UpdateManualToggleUI()
        {
            // 온습도 Entry 활성/비활성
            IndoorTempEntry.IsEnabled = _manualVisible;
            HumidityEntry.IsEnabled = _manualVisible;

            if (!_manualVisible)
            {
                // 자동감지 모드
                IndoorTempEntry.Text = string.Empty;
                IndoorTempEntry.Placeholder = "자동감지중";
                IndoorTempEntry.PlaceholderColor = Colors.Gray;

                HumidityEntry.Text = string.Empty;
                HumidityEntry.Placeholder = "자동감지중";
                HumidityEntry.PlaceholderColor = Colors.Gray;

                ManualToggleButton.Text = "온도·습도 수동입력";
            }
            else
            {
                // 수동입력 모드
                IndoorTempEntry.Placeholder = "예: 23.0";
                IndoorTempEntry.PlaceholderColor = Colors.LightGray;

                HumidityEntry.Placeholder = "예: 60";
                HumidityEntry.PlaceholderColor = Colors.LightGray;

                ManualToggleButton.Text = "온도·습도 자동감지";
            }
        }

        private void WaterTempEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            var newText = e.NewTextValue ?? string.Empty;

            // 매치 안 되면 이전 값으로 롤백
            if (!_twoIntOneDecimal.IsMatch(newText))
            {
                entry.TextChanged -= WaterTempEntryTextChanged;
                entry.Text = e.OldTextValue;
                entry.TextChanged += WaterTempEntryTextChanged;
            }
        }

        private void TurbidityEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            var newText = e.NewTextValue ?? string.Empty;

            if (_oneIntTwoDecimal.IsMatch(newText))
            {
                if (double.TryParse(newText, out var val))
                {
                    if (val >= 1.0)
                    {
                        // 자동으로 0.99로 바꾸기
                        entry.TextChanged -= TurbidityEntryTextChanged;
                        entry.Text = "0.99";
                        entry.TextChanged += TurbidityEntryTextChanged;
                    }
                }
            }
            else
            {
                // 포맷이 안 맞으면 롤백
                entry.TextChanged -= TurbidityEntryTextChanged;
                entry.Text = e.OldTextValue;
                entry.TextChanged += TurbidityEntryTextChanged;
            }
        }

        private void IndoorTempEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            var newText = e.NewTextValue ?? string.Empty;

            // 매치 안 되면 이전 값으로 롤백
            if (!_twoIntOneDecimal.IsMatch(newText))
            {
                entry.TextChanged -= IndoorTempEntryTextChanged;
                entry.Text = e.OldTextValue;
                entry.TextChanged += IndoorTempEntryTextChanged;
            }
        }

        private void HumidityEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            var newText = e.NewTextValue ?? string.Empty;

            // 매치 안 되면 이전 값으로 롤백
            if (!_twoIntOneDecimal.IsMatch(newText))
            {
                entry.TextChanged -= HumidityEntryTextChanged;
                entry.Text = e.OldTextValue;
                entry.TextChanged += HumidityEntryTextChanged;
            }
        }

        private void OnPageTapped(object sender, TappedEventArgs e)
        {
            // 모든 Entry에 대해 Unfocus (필요한 것만)
            WaterTempEntry?.Unfocus();
            TurbidityEntry?.Unfocus();
            IndoorTempEntry?.Unfocus();
            HumidityEntry?.Unfocus();
        }
    }
}
