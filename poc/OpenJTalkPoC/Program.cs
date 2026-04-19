using System.Diagnostics;
using OpenJTalkPoC;

// =============================================================================
// OpenJTalk PoC — 技術検証ドライバ
// backlog #46: jtalkDLL で WAV 生成・初期化時間実測・品質比較
//
// 実行前に SETUP.md の手順に従いファイルを配置してください。
// =============================================================================

const string Separator = new string('=', 60);

// ---- パス解決 ---------------------------------------------------------------

string baseDir  = AppContext.BaseDirectory;
string dataDir  = Path.Combine(baseDir, "Data", "openjtalk");
string dicPath  = Path.Combine(dataDir, "open_jtalk_dic_utf_8");
string voiceDir = Path.Combine(dataDir, "voice");

// voice は単一ファイル指定でも voiceDir 指定でも動作する。
// 優先順位: 1) Data/openjtalk/voice/mei_normal.htsvoice
//           2) Data/openjtalk/voice/ 以下の最初の .htsvoice
//           3) Data/openjtalk/ 以下の最初の .htsvoice
string? voicePath = FindVoice(voiceDir, dataDir);
string outputWav  = Path.Combine(baseDir, "output_test.wav");

// ---- ヘッダー表示 -----------------------------------------------------------

Console.WriteLine(Separator);
Console.WriteLine("OpenJTalk PoC — 技術検証");
Console.WriteLine($"実行日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine(Separator);
Console.WriteLine();
Console.WriteLine("[環境確認]");
Console.WriteLine($"  jtalk.dll : {Path.Combine(baseDir, "jtalk.dll")}");
Console.WriteLine($"  辞書      : {dicPath}");
Console.WriteLine($"  音声モデル: {voicePath ?? "(未検出)"}");
Console.WriteLine();

// ---- 前提ファイルチェック ---------------------------------------------------

bool prereqOk = true;

if (!NativeJTalk.IsDllPresent())
{
    PrintError("jtalk.dll が見つかりません。SETUP.md の手順を確認してください。");
    prereqOk = false;
}
if (!Directory.Exists(dicPath))
{
    PrintError($"辞書ディレクトリが見つかりません: {dicPath}");
    prereqOk = false;
}
if (voicePath == null)
{
    PrintError($"音声モデル (.htsvoice) が見つかりません。{voiceDir} 以下に配置してください。");
    prereqOk = false;
}

if (!prereqOk)
{
    Console.WriteLine();
    Console.WriteLine("前提ファイルが不足しています。SETUP.md を参照して配置後に再実行してください。");
    return 1;
}

// ---- [1] 初期化・時間計測 ---------------------------------------------------

Console.WriteLine("[1] 初期化");
var sw = Stopwatch.StartNew();
IntPtr handle = IntPtr.Zero;
try
{
    handle = NativeJTalk.Initialize(voicePath!, dicPath);
}
catch (DllNotFoundException ex)
{
    PrintError($"jtalk.dll のロードに失敗しました: {ex.Message}");
    PrintError("jtalk.dll が x64 ビルドであること、依存 DLL がすべて同じディレクトリにあることを確認してください。");
    return 1;
}
catch (Exception ex)
{
    PrintError($"初期化中に例外が発生しました: {ex.Message}");
    return 1;
}
sw.Stop();

long initMs = sw.ElapsedMilliseconds;
bool initOk = handle != IntPtr.Zero && initMs <= 5000;

Console.WriteLine($"  初期化時間 : {initMs} ms  {(initOk ? "✅" : "❌")} (基準: 5000 ms 以内)");

if (handle == IntPtr.Zero)
{
    PrintError("jtalk の初期化に失敗しました（ハンドルが null）。音声モデルと辞書のパスを確認してください。");
    return 1;
}

try
{
    // ---- [2] WAV 生成・時間計測 ----------------------------------------------

    Console.WriteLine();
    Console.WriteLine("[2] WAV 生成");

    var testCases = new[]
    {
        ("短文テスト",   "本日は晴天なり。"),
        ("業務文テスト", "声の広報テキスト読み上げツールの動作確認です。市長より市民の皆様へお知らせします。"),
    };

    bool wavOk = true;
    foreach (var (label, text) in testCases)
    {
        string outPath = Path.Combine(baseDir, $"output_{label}.wav");
        Console.WriteLine($"  [{label}]");
        Console.WriteLine($"    テキスト : {text}");

        sw.Restart();
        bool result = NativeJTalk.SpeakToFile(handle, text, outPath);
        sw.Stop();

        if (result && File.Exists(outPath))
        {
            long sizeKb = new FileInfo(outPath).Length / 1024;
            Console.WriteLine($"    結果     : ✅ 成功  {sizeKb} KB  生成時間: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"    出力先   : {outPath}");
        }
        else
        {
            Console.WriteLine($"    結果     : ❌ 失敗");
            wavOk = false;
        }
        Console.WriteLine();
    }

    // ---- [3] データサイズ確認 ------------------------------------------------

    Console.WriteLine("[3] データサイズ");
    ReportDirSize(dicPath,    "  MeCab 辞書      ");
    ReportFileSize(voicePath!, "  音声モデル      ");
    ReportFileSize(Path.Combine(baseDir, "jtalk.dll"), "  jtalk.dll       ");

    long totalMb = (DirBytes(dicPath) + FileBytes(voicePath!) + FileBytes(Path.Combine(baseDir, "jtalk.dll")))
                   / 1024 / 1024;
    Console.WriteLine($"  合計             : 約 {totalMb} MB");
    Console.WriteLine();

    // ---- [4] 判定サマリー ----------------------------------------------------

    Console.WriteLine(Separator);
    Console.WriteLine("[判定サマリー]");
    PrintResult("初期化時間 5 秒以内", initOk);
    PrintResult("WAV 生成成功",        wavOk);
    Console.WriteLine();
    Console.WriteLine("★ 音声品質は output_*.wav を再生して SAPI / WinRT と比較してください。");
    Console.WriteLine(Separator);
}
finally
{
    NativeJTalk.Clear(handle);
}

return 0;

// =============================================================================
// ローカル関数
// =============================================================================

static string? FindVoice(string voiceDir, string dataDir)
{
    // 優先: mei_normal.htsvoice
    string mei = Path.Combine(voiceDir, "mei_normal.htsvoice");
    if (File.Exists(mei)) return mei;

    // voiceDir 以下の最初の .htsvoice
    if (Directory.Exists(voiceDir))
    {
        string? found = Directory.GetFiles(voiceDir, "*.htsvoice", SearchOption.AllDirectories).FirstOrDefault();
        if (found != null) return found;
    }

    // dataDir 直下の最初の .htsvoice（フラット配置の場合）
    if (Directory.Exists(dataDir))
    {
        string? found = Directory.GetFiles(dataDir, "*.htsvoice", SearchOption.AllDirectories).FirstOrDefault();
        if (found != null) return found;
    }

    return null;
}

static void PrintError(string msg) => Console.WriteLine($"  ❌ {msg}");
static void PrintResult(string label, bool ok) => Console.WriteLine($"  {(ok ? "✅" : "❌")} {label}");

static long FileBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
static long DirBytes(string dir)   => Directory.Exists(dir)
    ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;

static void ReportFileSize(string path, string label)
{
    if (!File.Exists(path)) { Console.WriteLine($"{label}: (ファイルなし)"); return; }
    Console.WriteLine($"{label}: {new FileInfo(path).Length / 1024.0 / 1024.0:F1} MB");
}

static void ReportDirSize(string dir, string label)
{
    if (!Directory.Exists(dir)) { Console.WriteLine($"{label}: (ディレクトリなし)"); return; }
    long mb = DirBytes(dir) / 1024 / 1024;
    Console.WriteLine($"{label}: 約 {mb} MB");
}
