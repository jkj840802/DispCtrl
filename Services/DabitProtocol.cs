using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DispCtrl.Services
{
    /// <summary>
    /// 다빛 ASCII 프로토콜용 명령 문자열 생성기
    /// </summary>
    public static class DabitProtocol
    {
        /// <summary>
        /// 화면에 텍스트 출력 (#TEXT[...]$)
        /// </summary>
        /// <param name="text">표시할 메시지 (최대 길이 매뉴얼 참조)</param>
        public static string CreateTextCommand(string text)
            => $"{text}";

        /// <summary>
        /// 화면 전체 지우기 (#CLR[]$)
        /// </summary>
        public static string CreateClearCommand()
            => "#CLR[]$";

        /// <summary>
        /// 밝기 설정 (#BRT[level]$)
        /// level: 0(꺼짐)–7(최대), 매뉴얼 참조
        /// </summary>
        public static string CreateBrightnessCommand(int level)
            => $"#BRT[{level}]$";
    }
}
