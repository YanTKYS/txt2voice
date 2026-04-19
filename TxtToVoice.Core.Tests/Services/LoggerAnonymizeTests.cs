using TxtToVoice.Services;
using Xunit;

namespace TxtToVoice.Tests.Services
{
    /// <summary>
    /// Logger.AnonymizePaths() の正規表現を検証するテスト（#38/#39）。
    /// AnonymizePaths は internal のため InternalsVisibleTo("TxtToVoice.Tests") が必要。
    /// </summary>
    public class LoggerAnonymizeTests
    {
        private static string Anon(string msg) => Logger.AnonymizePaths(msg);

        // ================================================================
        // 通常のドライブレターパス（空白なし）
        // ================================================================

        [Fact]
        public void ドライブレターパスをファイル名のみに置換する()
        {
            Assert.Equal("原稿.txt", Anon(@"C:\Users\田中\Desktop\原稿.txt"));
        }

        [Fact]
        public void メッセージの一部に含まれるパスを置換する()
        {
            string result = Anon(@"ファイル読み込み失敗: C:\path\file.txt の処理中");
            Assert.DoesNotContain(@"C:\path\", result);
            Assert.Contains("file.txt", result);
        }

        [Fact]
        public void パスが含まれないメッセージはそのまま返す()
        {
            Assert.Equal("エラーが発生しました", Anon("エラーが発生しました"));
        }

        // ================================================================
        // シングルクォート付きパス（.NET 例外メッセージに多い、空白含む）
        // ================================================================

        [Fact]
        public void シングルクォート付き通常パスを置換する()
        {
            string result = Anon(@"Could not find file 'C:\Users\田中\原稿.txt'.");
            Assert.Equal("Could not find file '原稿.txt'.", result);
        }

        [Fact]
        public void シングルクォート付き空白含むパスを置換する()
        {
            string result = Anon(@"File 'C:\Users\A\My Documents\原稿.txt' not found.");
            Assert.Equal("File '原稿.txt' not found.", result);
        }

        [Fact]
        public void シングルクォート付きUNCパスを置換する()
        {
            string result = Anon(@"Error: '\\server\share\原稿.txt'");
            Assert.Equal("Error: '原稿.txt'", result);
        }

        // ================================================================
        // ダブルクォート付きパス
        // ================================================================

        [Fact]
        public void ダブルクォート付き通常パスを置換する()
        {
            string result = Anon("保存先: \"C:\\path\\file.txt\"");
            Assert.Equal("保存先: \"file.txt\"", result);
        }

        [Fact]
        public void ダブルクォート付き空白含むパスを置換する()
        {
            string result = Anon("パス: \"C:\\My Folder\\file.txt\" を開きます");
            Assert.Equal("パス: \"file.txt\" を開きます", result);
        }

        // ================================================================
        // UNC パス（クォートなし）
        // ================================================================

        [Fact]
        public void クォートなしUNCパスを置換する()
        {
            string result = Anon(@"保存失敗: \\server01\share\docs\原稿.txt");
            Assert.DoesNotContain(@"\\server01\share\docs\", result);
            Assert.Contains("原稿.txt", result);
        }

        // ================================================================
        // 複数パスの置換
        // ================================================================

        [Fact]
        public void メッセージ内の複数パスをすべて置換する()
        {
            string result = Anon(@"コピー元: C:\src\file.txt、コピー先: C:\dst\file.txt");
            Assert.DoesNotContain(@"C:\src\", result);
            Assert.DoesNotContain(@"C:\dst\", result);
        }
    }
}
