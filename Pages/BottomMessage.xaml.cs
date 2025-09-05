using Microsoft.Maui.Controls;
using System;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace DispCtrl
{
    public partial class BottomMessagePage : ContentPage
    {
        const double MaxUnits = 18.0;
        const double TempMaxUnits = MaxUnits + 2.0;  // 한 박자 여유(19단위)

        private void OnButtonEntryTextChanged(object? sender, TextChangedEventArgs e) => UpdateUnitLabelAndEnforceLimit(sender, e, OnButtonEntryTextChanged, BottomMessageUnitLabel);

        // 전송 성공 여부 누적
        private bool ok = true;

        public BottomMessagePage()
        {
            InitializeComponent();
            // 기본 색상은 노란색(/C3)
            BottomColorPicker.SelectedItem = "/C3";
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

        private async void OnBottomSendClicked(object sender, EventArgs e)
        {
            // 1) 메시지 입력 검증
            var msg = TrimToMaxUnits(BottomMessageEntry.Text?.Trim() ?? string.Empty);
            if (msg.Length == 0)
            {
                await DisplayAlert("알림", "메시지를 입력해 주세요.", "확인");
                return;
            }

            // 2) 색상 코드 가져오기
            var selectedName = BottomColorPicker.SelectedItem as string ?? "노랑색";
            var colorCode = _colorMap.TryGetValue(selectedName, out var code) ? code : "/C3"; // 기본은 노랑

            // 3) 페이로드 조립 (하단 메시지는 P0004 사용)
            var payload = $"1/P0002/F0203/X0072/Y0812/S0099{colorCode}{msg}";

            // 4) 전송
            var StationResp = await WiFiSender.SendCommandToHostAsync(1, payload);
            ok &= StationResp;

            Preferences.Default.Set("ButtonMsg", colorCode + msg);

            // 5) 잠시 대기 후 상태 표시
            await Task.Delay(10);
            BottomStatusLabel.Text = StationResp
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

        private void BottomColorPickerButton_Clicked(object sender, EventArgs e)
        {
            // 버튼을 누르면 숨어있던 Picker에 포커스를 주어 선택 창을 강제로 엽니다.
            BottomColorPicker.Focus();
        }

        private void BottomColorPicker_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (BottomColorPicker.SelectedIndex != -1)
            {
                var selected = BottomColorPicker.SelectedItem.ToString();
                BottomColorEntry.Text = selected;

                BottomColorEntry.TextColor = selected switch
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

        private void OnBottomMessageUnfocused(object sender, FocusEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            entry.Text = PadToFixedLength(entry.Text, 16);

            // 다시 계산 후 라벨 표시
            string text = entry.Text ?? "";
            int spaceCount = text.Count(c => c == ' ');
            int charCount = text.Length - spaceCount;
            double totalUnits = charCount + spaceCount * 0.5;

            BottomMessageUnitLabel.Text = $"{totalUnits:0.##} / 16";
        }

        private string PadToFixedLength(string? input, double maxUnits)
        {
            var text = input ?? string.Empty;

            int spaceCount = text.Count(c => c == ' ');
            int charCount = text.Length - spaceCount;
            double units = charCount + spaceCount * 0.5;

            if (units >= maxUnits)
                return text;

            int spacesToAdd = (int)((maxUnits - units) * 2);
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
