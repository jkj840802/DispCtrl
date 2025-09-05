using System;
using System.Text;
using Microsoft.Maui.Controls;

namespace DispCtrl
{
    public partial class TitleMessagePage : ContentPage
    {
        const double MaxUnits = 12.0;
        const double TempMaxUnits = MaxUnits + 2.0;  // 한 박자 여유(19단위)

        private void OnTitleEntryTextChanged(object? sender, TextChangedEventArgs e) => UpdateUnitLabelAndEnforceLimit(sender, e, OnTitleEntryTextChanged, TitleMessageUnitLabel);

        public TitleMessagePage()
        {
            InitializeComponent();
            // 기본 색상은 노란색(/C3)
            TitleColorPicker.SelectedItem = "/C3";
        }

        readonly Dictionary<string, string> _colorMap = new()
        {
            { "빨강색", "/C1" },
            { "초록색", "/C2" },
            { "노랑색", "/C3" },
            { "파랑색", "/C4" },
            { "자주색", "/C5" },
            { "청록색", "/C6" },
            { "흰색", "/C7" }
        };

        private async void OnTitleSendClicked(object sender, EventArgs e)
        {
            // 1) 제목 검증
            var titleText = TrimToMaxUnits(TitleTextEntry.Text?.Trim() ?? string.Empty);
            if (titleText.Length == 0)
            {
                await DisplayAlert("알림", "제목을 입력해 주세요.", "확인");
                return;
            }

            var selectedName = TitleColorPicker.SelectedItem as string ?? "노랑색";
            var colorCode = _colorMap.TryGetValue(selectedName, out var code) ? code : "/C3"; // 기본은 노랑

            // 3) 페이로드 조합
            // 1) 첫 번째 명령(재시도)
            // 1) 첫 번째 명령
            bool stationResp = false;

            
            string cmd1 = $"0/P0000/F0203/X0048/Y0008{colorCode}{TitleTextEntry.Text}/C3{DisplaySettingsPage.DayPacketData}";
            while (true)
            {
                var resp1 = await WiFiSender.SendAndReceiveAsync(0, cmd1);
                Console.WriteLine($"[WF] Title-500-P0000 응답: {resp1}");
                if (resp1 is "![0000!]" or "![0010!]")
                    break;               // 성공이면 다음으로
                await Task.Delay(1000);   // 실패·타임아웃 시 재시도
            }
            // 2) 세 번째 명령
            string cmd3 = $"0/P0001/F0205/X4872/Y0008/C3{DisplaySettingsPage.TimePacketData}";
            while (true)
            {
                var resp3 = await WiFiSender.SendAndReceiveAsync(0, cmd3);
                Console.WriteLine($"[WF] Title-500-P0002 응답: {resp3}");
                if (resp3 is "![0000!]" or "![0010!]")
                    break;
                await Task.Delay(1000);
                stationResp = true;
            }

            // 3) 시간 요청 / 응답 받기 (원본 그대로)
            string requestPacket = "30" + DisplaySettingsPage.MakeControllerTimePacket();
            string lTime = await WiFiSender.SendAndReceiveAsync(0, requestPacket) ?? string.Empty;
            Console.WriteLine($"[WF] ? Title-LTime (채널 0): {lTime}");
            await Task.Delay(100);

            // 3) 두 개 모두 성공했으니 stationResp == true
            await Task.Delay(10);
            TitleStatusLabel.Text = stationResp
                ? "전송 성공"
                : "전송 실패";
        }

        private string TrimToMaxUnits(string text)
        {
            double units = 0;
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                units += c == ' ' ? 0.5 : 1.0;
                if (units > MaxUnits) break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void OnTitleEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            string newText = entry.Text ?? string.Empty;

            // 단위 계산 (띄어쓰기 0.5)
            double totalUnits = 0;
            int validLength = 0;

            for (int i = 0; i < newText.Length; i++)
            {
                char c = newText[i];
                totalUnits += (c == ' ') ? 0.5 : 1.0;

                if (totalUnits > 12.0)
                    break;

                validLength = i + 1;
            }

            // 잘라내기
            if (newText.Length != validLength)
            {
                newText = newText.Substring(0, validLength);
            }

            // 텍스트 보정 (총 12단위에 맞게 공백 패딩)
            entry.Text = PadToFixedLength(newText);

            // 단위 표시
            TitleMessageUnitLabel.Text = $"{totalUnits:0.##} / 12";
        }

        private void TitleColorPickerButton_Clicked(object sender, EventArgs e)
        {
            // 버튼을 누르면 숨어있던 Picker에 포커스를 주어 선택 창을 강제로 엽니다.
            TitleColorPicker.Focus();
        }

        private void TitleColorPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TitleColorPicker.SelectedIndex != -1)
            {
                var selected = TitleColorPicker.SelectedItem.ToString();
                TitleColorEntry.Text = selected;

                TitleColorEntry.TextColor = selected switch
                {
                    "빨강색" => Colors.Red,
                    "초록색" => Colors.Green,
                    "노랑색" => Color.FromArgb("#FFD700"),
                    "파랑색" => Colors.Blue,
                    "자주색" => Color.FromArgb("#800080"), // 보라색
                    "청록색" => Color.FromArgb("#008080"), // 청록색
                    "흰색" => Colors.White,
                    _ => Colors.Black // 기본값
                };
            }
        }

        /// <summary>
        /// 한글·일반문자 1단위, 공백 0.5단위로 계산해서
        /// 총 12단위가 되도록 뒤에 스페이스를 채워 반환합니다.
        /// </summary>
        private string PadToFixedLength(string? input)
        {
            var text = input ?? string.Empty;

            // 공백 개수, 공백 외 글자 개수
            int spaceCount = text.Count(c => c == ' ');
            int charCount = text.Length - spaceCount;

            // 총 단위 계산
            double units = charCount + spaceCount * 0.5;

            // 이미 12단위 이상이면 그대로
            if (units >= 12)
                return text;

            // 부족한 단위를 공백(0.5단위)로 채우려면 spaces = (12 - units) / 0.5
            int spacesToAdd = (int)((12 - units) * 2);

            return text + new string(' ', spacesToAdd);
        }

        private void UpdateUnitLabelAndEnforceLimit(object? sender, TextChangedEventArgs e, EventHandler<TextChangedEventArgs> handler, Label targetLabel)
        {
            {
                if (sender is not Entry entry || handler == null)
                    return;

                var newText = e.NewTextValue ?? string.Empty;
                var oldText = e.OldTextValue ?? string.Empty;

                int spaceCount = newText.Count(c => c == ' ');
                int charCount = newText.Length - spaceCount;
                double totalUnits = charCount + spaceCount * 0.5;

                // 1) 19단위(TempMaxUnits) 초과하면 즉시 롤백
                if (totalUnits > TempMaxUnits)
                {
                    entry.TextChanged -= handler;
                    entry.Text = oldText;
                    entry.TextChanged += handler;
                    totalUnits = TempMaxUnits;
                }
                // 2) 18단위(=MaxUnits) 초과 & 19단위 이하면
                //    -> IME 완성 뒤 클램프로직을 메인스레드에 연기 배치
                else if (totalUnits > MaxUnits)
                {
                    // IME 조합이 끝난 뒤 18단위로 클램프
                    MainThread.BeginInvokeOnMainThread(() => {
                        var trimmed = TrimToMaxUnits(entry.Text ?? string.Empty);
                        entry.TextChanged -= handler;
                        entry.Text = trimmed;
                        entry.TextChanged += handler;
                    });
                }

                // 3) 레이블에는 항상 0~18단위 구간만 표시
                var displayUnits = Math.Min(totalUnits, MaxUnits);
                targetLabel.Text = $"{displayUnits:0.#} / {MaxUnits}";
            }
        }
    }
}
