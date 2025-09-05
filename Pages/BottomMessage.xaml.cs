using Microsoft.Maui.Controls;
using System;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace DispCtrl
{
    public partial class BottomMessagePage : ContentPage
    {
        const double MaxUnits = 18.0;
        const double TempMaxUnits = MaxUnits + 2.0;  // �� ���� ����(19����)

        private void OnButtonEntryTextChanged(object? sender, TextChangedEventArgs e) => UpdateUnitLabelAndEnforceLimit(sender, e, OnButtonEntryTextChanged, BottomMessageUnitLabel);

        // ���� ���� ���� ����
        private bool ok = true;

        public BottomMessagePage()
        {
            InitializeComponent();
            // �⺻ ������ �����(/C3)
            BottomColorPicker.SelectedItem = "/C3";
        }

        readonly Dictionary<string, string> _colorMap = new()
        {
            { "������", "/C1" },
            { "�ʷϻ�", "/C2" },
            { "�����", "/C3" },
            { "�Ķ���", "/C4" },
            { "���ֻ�", "/C5" },
            { "û�ϻ�", "/C6" },
            { "���", "/C7" }
        };

        private async void OnBottomSendClicked(object sender, EventArgs e)
        {
            // 1) �޽��� �Է� ����
            var msg = TrimToMaxUnits(BottomMessageEntry.Text?.Trim() ?? string.Empty);
            if (msg.Length == 0)
            {
                await DisplayAlert("�˸�", "�޽����� �Է��� �ּ���.", "Ȯ��");
                return;
            }

            // 2) ���� �ڵ� ��������
            var selectedName = BottomColorPicker.SelectedItem as string ?? "�����";
            var colorCode = _colorMap.TryGetValue(selectedName, out var code) ? code : "/C3"; // �⺻�� ���

            // 3) ���̷ε� ���� (�ϴ� �޽����� P0004 ���)
            var payload = $"1/P0002/F0203/X0072/Y0812/S0099{colorCode}{msg}";

            // 4) ����
            var StationResp = await WiFiSender.SendCommandToHostAsync(1, payload);
            ok &= StationResp;

            Preferences.Default.Set("ButtonMsg", colorCode + msg);

            // 5) ��� ��� �� ���� ǥ��
            await Task.Delay(10);
            BottomStatusLabel.Text = StationResp
                ? "���� ����"
                : "���� ����";
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
            // ��ư�� ������ �����ִ� Picker�� ��Ŀ���� �־� ���� â�� ������ ���ϴ�.
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
                    "������" => Colors.Red,
                    "�ʷϻ�" => Colors.Green,
                    "�����" => Color.FromArgb("#FFD700"),
                    "�Ķ���" => Colors.Blue,
                    "���ֻ�" => Color.FromArgb("#800080"), // �����
                    "û�ϻ�" => Color.FromArgb("#008080"), // û�ϻ�
                    "���" => Colors.White,
                    _ => Colors.Black // �⺻��
                };
            }
        }

        private void OnBottomMessageUnfocused(object sender, FocusEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            entry.Text = PadToFixedLength(entry.Text, 16);

            // �ٽ� ��� �� �� ǥ��
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

                // 1) 19����(TempMaxUnits) �ʰ��ϸ� ��� �ѹ�
                if (totalUnits > TempMaxUnits)
                {
                    entry.TextChanged -= handler;
                    entry.Text = oldText;
                    entry.TextChanged += handler;
                    totalUnits = TempMaxUnits;
                }
                // 2) 18����(=MaxUnits) �ʰ� & 19���� ���ϸ�
                //    -> IME �ϼ� �� Ŭ���������� ���ν����忡 ���� ��ġ
                else if (totalUnits > MaxUnits)
                {
                    // IME ������ ���� �� 18������ Ŭ����
                    MainThread.BeginInvokeOnMainThread(() => {
                        var trimmed = TrimToMaxUnits(entry.Text ?? string.Empty);
                        entry.TextChanged -= handler;
                        entry.Text = trimmed;
                        entry.TextChanged += handler;
                    });
                }

                // 3) ���̺��� �׻� 0~18���� ������ ǥ��
                var displayUnits = Math.Min(totalUnits, MaxUnits);
                targetLabel.Text = $"{displayUnits:0.#} / {MaxUnits}";
            }
        }
    }
}
