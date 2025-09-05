using System;
using System.Text;
using System.Linq;                 // ★ 추가: LINQ
using Microsoft.Maui.Controls;

namespace DispCtrl
{
    public partial class UrgentMessagePage : ContentPage
    {
        private const double MaxUnits = 18.0;
        const double TempMaxUnits = MaxUnits + 2.0;  // 한 박자 여유(19단위)

        private void OnMessageEntry1TextChanged(object? sender, TextChangedEventArgs e)
                => UpdateUnitLabelAndEnforceLimit(sender, e, OnMessageEntry1TextChanged, MessageUnitLabel1);

        private void OnMessageEntry2TextChanged(object? sender, TextChangedEventArgs e)
            => UpdateUnitLabelAndEnforceLimit(sender, e, OnMessageEntry2TextChanged, MessageUnitLabel2);

        private void OnMessageEntry3TextChanged(object? sender, TextChangedEventArgs e)
            => UpdateUnitLabelAndEnforceLimit(sender, e, OnMessageEntry3TextChanged, MessageUnitLabel3);

        public UrgentMessagePage()
        {
            InitializeComponent();

            MessageUnitLabel1.Text = MessageUnitLabel2.Text = MessageUnitLabel3.Text = $"0 / {MaxUnits}";

            // 피커에 표시할 텍스트만 바인딩
            UrgentColorPicker1.ItemsSource = _colorList.Select(c => c.Label).ToList();
            UrgentColorPicker2.ItemsSource = _colorList.Select(c => c.Label).ToList();
            UrgentColorPicker3.ItemsSource = _colorList.Select(c => c.Label).ToList();

            // 기본 선택 인덱스 (선택 안 함)
            UrgentColorPicker1.SelectedIndex = -1;
            UrgentColorPicker2.SelectedIndex = -1;
            UrgentColorPicker3.SelectedIndex = -1;
        }

        private readonly List<(string Label, string Code)> _colorList = new()
        {
            ("빨강 (Red)", "/C1"),
            ("초록 (Green)", "/C2"),
            ("노랑 (Yellow)", "/C3"),
            ("파랑 (Blue)", "/C4"),
            ("자주 (Magenta)", "/C5"),
            ("청록 (Cyan)", "/C6"),
            ("흰색 (White)", "/C7"),
        };

        // 메시지 1 전송
        private async void OnSendUrgent1Clicked(object sender, EventArgs e)
        {
            var label = UrgentColorPicker1.SelectedItem as string;
            await SendUrgentMessageAsync(MessageEntry1.Text, label, 0, "Y0004");
        }

        private async void OnSendUrgent2Clicked(object sender, EventArgs e)
        {
            var label = UrgentColorPicker2.SelectedItem as string;
            await SendUrgentMessageAsync(MessageEntry2.Text, label, 1, "Y0408");
        }

        private async void OnSendUrgent3Clicked(object sender, EventArgs e)
        {
            var label = UrgentColorPicker3.SelectedItem as string;
            await SendUrgentMessageAsync(MessageEntry3.Text, label, 2, "Y0812");
        }

        // 공통 전송 로직
        private async Task SendUrgentMessageAsync(string rawMsg, string? rawLabel, int seqNumber, string Y_Value)
        {
            bool ok = true;

            // 메시지 비었는지 확인
            var msg = TrimToMaxUnits(rawMsg?.Trim() ?? string.Empty);
            if (msg.Length == 0)
            {
                // ★ 표기 수정: seqNumber는 0/1/2 → 사용자 표시는 1/2/3
                await DisplayAlert("알림", $"{seqNumber + 1}번 메시지를 입력해 주세요.", "확인");
                return;
            }

            // 색상 라벨 → 코드 변환 (못 찾으면 기본 /C3)
            var match = _colorList.FirstOrDefault(c => c.Label == rawLabel);
            string colorCode = !string.IsNullOrEmpty(match.Code) ? match.Code : "/C3";

            // 페이로드 조립
            var payload = $"1/P000{seqNumber}/F0203/X0072/{Y_Value}{colorCode}{msg}";

            var resp = await WiFiSender.SendCommandToHostAsync(1, payload);
            ok &= resp;

            // 결과 표시
            UrgentStatusLabel.Text = ok ? "✓ 전송 성공" : "✗ 전송 실패";
            UrgentStatusLabel.TextColor = ok ? Colors.Green : Colors.Red;
        }

        private string TrimToMaxUnits(string text)
        {
            double units = 0;
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                units += c == ' ' ? 0.5 : 1.0;
                if (units > 18) break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ★ 공용: 표시 문자열에서 색상명 추출 (괄호 안 영문 우선, 없으면 첫 단어)
        private static string? ExtractColorName(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return null;

            return label.Contains('(')
                ? label[(label.IndexOf('(') + 1)..].TrimEnd(')').Trim()
                : label.Split(' ')[0];
        }

        // ★ 공용: 색상명 → Color 매핑 (catch-all은 마지막)
        private static Color ColorFromName(string? nameOrAlias)
        {
            if (string.IsNullOrWhiteSpace(nameOrAlias))
                return Colors.Black;

            var key = nameOrAlias.Trim().ToLowerInvariant();

            return key switch
            {
                // 한국어
                "빨강" => Colors.Red,
                "초록" => Colors.Green,
                "노랑" => Colors.Yellow,
                "파랑" => Colors.Blue,
                "자주" => Colors.Magenta,
                "청록" => Colors.Cyan,
                "흰색" => Colors.White,

                // 영어(옵션)
                "red" => Colors.Red,
                "green" => Colors.Green,
                "yellow" => Colors.Yellow,
                "blue" => Colors.Blue,
                "magenta" or "purple" => Colors.Magenta,
                "cyan" or "teal" => Colors.Cyan,
                "white" => Colors.White,

                _ => Colors.Black
            };
        }

        private void OnUrgentColorPicker1Changed(object sender, EventArgs e)
        {
            if (UrgentColorPicker1.SelectedIndex != -1)
            {
                var selectedItemText = UrgentColorPicker1.SelectedItem?.ToString();
                Picker1Label.Text = selectedItemText;

                var colorName = ExtractColorName(selectedItemText);
                Picker1Label.TextColor = ColorFromName(colorName);  // ★ switch 제거
            }
        }

        private void OnUrgentColorPicker2Changed(object sender, EventArgs e)
        {
            if (UrgentColorPicker2.SelectedIndex != -1)
            {
                var selectedItemText = UrgentColorPicker2.SelectedItem?.ToString();
                Picker2Label.Text = selectedItemText;

                var colorName = ExtractColorName(selectedItemText);
                Picker2Label.TextColor = ColorFromName(colorName);  // ★ switch 제거
            }
        }

        private void OnUrgentColorPicker3Changed(object sender, EventArgs e)
        {
            if (UrgentColorPicker3.SelectedIndex != -1)
            {
                var selectedItemText = UrgentColorPicker3.SelectedItem?.ToString();
                Picker3Label.Text = selectedItemText;

                var colorName = ExtractColorName(selectedItemText);
                Picker3Label.TextColor = ColorFromName(colorName);  // ★ switch 제거
            }
        }

        private void PickerButton1_Clicked(object sender, EventArgs e)
        {
            UrgentColorPicker1.Focus();
        }

        private void PickerButton2_Clicked(object sender, EventArgs e)
        {
            UrgentColorPicker2.Focus();
        }

        private void PickerButton3_Clicked(object sender, EventArgs e)
        {
            UrgentColorPicker3.Focus();
        }

        private void UpdateUnitLabelAndEnforceLimit(object? sender, TextChangedEventArgs e, EventHandler<TextChangedEventArgs> handler, Label targetLabel)
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
            // 2) 18단위 초과 & 19단위 이하 → IME 완성 뒤 클램프
            else if (totalUnits > MaxUnits)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var trimmed = TrimToMaxUnits(entry.Text ?? string.Empty);
                    entry.TextChanged -= handler;
                    entry.Text = trimmed;
                    entry.TextChanged += handler;
                });
            }

            // 3) 레이블에는 0~18단위 구간만 표시
            var displayUnits = Math.Min(totalUnits, MaxUnits);
            targetLabel.Text = $"{displayUnits:0.#} / {MaxUnits}";
        }

        private void OnBackgroundTapped(object sender, TappedEventArgs e)
        {
            MessageEntry1?.Unfocus();
            MessageEntry2?.Unfocus();
            MessageEntry3?.Unfocus();
        }
    }
}