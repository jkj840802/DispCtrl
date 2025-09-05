using System;
using System.Text;
using Microsoft.Maui.Controls;

namespace DispCtrl
{
    public partial class TitleMessagePage : ContentPage
    {
        const double MaxUnits = 12.0;
        const double TempMaxUnits = MaxUnits + 2.0;  // �� ���� ����(19����)

        private void OnTitleEntryTextChanged(object? sender, TextChangedEventArgs e) => UpdateUnitLabelAndEnforceLimit(sender, e, OnTitleEntryTextChanged, TitleMessageUnitLabel);

        public TitleMessagePage()
        {
            InitializeComponent();
            // �⺻ ������ �����(/C3)
            TitleColorPicker.SelectedItem = "/C3";
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

        private async void OnTitleSendClicked(object sender, EventArgs e)
        {
            // 1) ���� ����
            var titleText = TrimToMaxUnits(TitleTextEntry.Text?.Trim() ?? string.Empty);
            if (titleText.Length == 0)
            {
                await DisplayAlert("�˸�", "������ �Է��� �ּ���.", "Ȯ��");
                return;
            }

            var selectedName = TitleColorPicker.SelectedItem as string ?? "�����";
            var colorCode = _colorMap.TryGetValue(selectedName, out var code) ? code : "/C3"; // �⺻�� ���

            // 3) ���̷ε� ����
            // 1) ù ��° ���(��õ�)
            // 1) ù ��° ���
            bool stationResp = false;

            
            string cmd1 = $"0/P0000/F0203/X0048/Y0008{colorCode}{TitleTextEntry.Text}/C3{DisplaySettingsPage.DayPacketData}";
            while (true)
            {
                var resp1 = await WiFiSender.SendAndReceiveAsync(0, cmd1);
                Console.WriteLine($"[WF] Title-500-P0000 ����: {resp1}");
                if (resp1 is "![0000!]" or "![0010!]")
                    break;               // �����̸� ��������
                await Task.Delay(1000);   // ���С�Ÿ�Ӿƿ� �� ��õ�
            }
            // 2) �� ��° ���
            string cmd3 = $"0/P0001/F0205/X4872/Y0008/C3{DisplaySettingsPage.TimePacketData}";
            while (true)
            {
                var resp3 = await WiFiSender.SendAndReceiveAsync(0, cmd3);
                Console.WriteLine($"[WF] Title-500-P0002 ����: {resp3}");
                if (resp3 is "![0000!]" or "![0010!]")
                    break;
                await Task.Delay(1000);
                stationResp = true;
            }

            // 3) �ð� ��û / ���� �ޱ� (���� �״��)
            string requestPacket = "30" + DisplaySettingsPage.MakeControllerTimePacket();
            string lTime = await WiFiSender.SendAndReceiveAsync(0, requestPacket) ?? string.Empty;
            Console.WriteLine($"[WF] ? Title-LTime (ä�� 0): {lTime}");
            await Task.Delay(100);

            // 3) �� �� ��� ���������� stationResp == true
            await Task.Delay(10);
            TitleStatusLabel.Text = stationResp
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

        private void OnTitleEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry)
                return;

            string newText = entry.Text ?? string.Empty;

            // ���� ��� (���� 0.5)
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

            // �߶󳻱�
            if (newText.Length != validLength)
            {
                newText = newText.Substring(0, validLength);
            }

            // �ؽ�Ʈ ���� (�� 12������ �°� ���� �е�)
            entry.Text = PadToFixedLength(newText);

            // ���� ǥ��
            TitleMessageUnitLabel.Text = $"{totalUnits:0.##} / 12";
        }

        private void TitleColorPickerButton_Clicked(object sender, EventArgs e)
        {
            // ��ư�� ������ �����ִ� Picker�� ��Ŀ���� �־� ���� â�� ������ ���ϴ�.
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

        /// <summary>
        /// �ѱۡ��Ϲݹ��� 1����, ���� 0.5������ ����ؼ�
        /// �� 12������ �ǵ��� �ڿ� �����̽��� ä�� ��ȯ�մϴ�.
        /// </summary>
        private string PadToFixedLength(string? input)
        {
            var text = input ?? string.Empty;

            // ���� ����, ���� �� ���� ����
            int spaceCount = text.Count(c => c == ' ');
            int charCount = text.Length - spaceCount;

            // �� ���� ���
            double units = charCount + spaceCount * 0.5;

            // �̹� 12���� �̻��̸� �״��
            if (units >= 12)
                return text;

            // ������ ������ ����(0.5����)�� ä����� spaces = (12 - units) / 0.5
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
