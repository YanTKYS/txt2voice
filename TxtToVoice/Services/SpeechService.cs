using System;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;

namespace TxtToVoice.Services
{
    /// <summary>
    /// System.Speech.Synthesis.SpeechSynthesizer のラッパー。
    /// 読み上げの開始・一時停止・再開・停止・WAV保存を提供する。
    /// </summary>
    public class SpeechService : IDisposable
    {
        private SpeechSynthesizer _synth;
        private bool _disposed;

        // UI スレッドへのディスパッチ用
        private readonly SynchronizationContext? _uiContext;

        // ----------------------------------------------------------------
        // イベント
        // ----------------------------------------------------------------

        /// <summary>読み上げ開始時に発火（UI スレッドで呼ばれる）</summary>
        public event EventHandler? SpeakStarted;

        /// <summary>読み上げ完了・中断時に発火（UI スレッドで呼ばれる）</summary>
        public event EventHandler? SpeakCompleted;

        /// <summary>エラー発生時に発火（UI スレッドで呼ばれる）。引数はエラーメッセージ</summary>
        public event EventHandler<string>? SpeakError;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        public SpeechService()
        {
            _uiContext = SynchronizationContext.Current;
            _synth = CreateSynthesizer();
        }

        private SpeechSynthesizer CreateSynthesizer()
        {
            var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.SpeakStarted   += (s, e) => RaiseOnUiThread(() => SpeakStarted?.Invoke(this, EventArgs.Empty));
            synth.SpeakCompleted += (s, e) => RaiseOnUiThread(() => SpeakCompleted?.Invoke(this, EventArgs.Empty));
            return synth;
        }

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        public SynthesizerState State => _disposed ? SynthesizerState.Ready : _synth.State;

        // ----------------------------------------------------------------
        // 音声・パラメータ設定
        // ----------------------------------------------------------------

        /// <summary>インストール済みの有効な音声名の一覧を返す。</summary>
        public IEnumerable<string> GetAvailableVoices()
        {
            var list = new List<string>();
            foreach (var v in _synth.GetInstalledVoices())
                if (v.Enabled) list.Add(v.VoiceInfo.Name);
            return list;
        }

        /// <summary>使用する音声を名前で指定する。</summary>
        public void SetVoice(string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName)) return;
            try   { _synth.SelectVoice(voiceName); }
            catch (Exception ex) { Logger.Warn($"音声選択失敗: {voiceName} / {ex.Message}"); }
        }

        /// <summary>現在の音声名を返す。</summary>
        public string CurrentVoiceName => _synth.Voice?.Name ?? string.Empty;

        /// <summary>
        /// 読み上げ速度を設定する。
        /// -10（最遅）〜 0（標準）〜 10（最速）
        /// </summary>
        public void SetRate(int rate)
        {
            _synth.Rate = Math.Clamp(rate, -10, 10);
        }

        /// <summary>
        /// 音量を設定する。
        /// 0〜100
        /// </summary>
        public void SetVolume(int volume)
        {
            _synth.Volume = Math.Clamp(volume, 0, 100);
        }

        // ----------------------------------------------------------------
        // 再生操作
        // ----------------------------------------------------------------

        /// <summary>テキストを非同期で読み上げる。</summary>
        public void SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                RaiseOnUiThread(() => SpeakError?.Invoke(this, "読み上げるテキストがありません。"));
                return;
            }

            try
            {
                _synth.SpeakAsyncCancelAll();
                _synth.SetOutputToDefaultAudioDevice();
                _synth.SpeakAsync(text);
                Logger.Info($"読み上げ開始: {text.Length}文字");
            }
            catch (Exception ex)
            {
                Logger.Error($"読み上げエラー: {ex.Message}");
                RaiseOnUiThread(() => SpeakError?.Invoke(this, $"読み上げ中にエラーが発生しました。\n{ex.Message}"));
            }
        }

        /// <summary>読み上げを一時停止する。</summary>
        public void Pause()
        {
            if (_synth.State == SynthesizerState.Speaking)
            {
                _synth.Pause();
                Logger.Info("読み上げ一時停止");
            }
        }

        /// <summary>一時停止中の読み上げを再開する。</summary>
        public void Resume()
        {
            if (_synth.State == SynthesizerState.Paused)
            {
                _synth.Resume();
                Logger.Info("読み上げ再開");
            }
        }

        /// <summary>読み上げを停止する。</summary>
        public void Stop()
        {
            _synth.SpeakAsyncCancelAll();
            Logger.Info("読み上げ停止");
        }

        // ----------------------------------------------------------------
        // WAV 保存
        // ----------------------------------------------------------------

        /// <summary>
        /// テキストを WAV ファイルとして保存する（同期処理）。
        /// 現在の音声・速度・音量設定を引き継ぐ。
        /// </summary>
        public void SaveToWav(string text, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("読み上げるテキストがありません。");

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // WAV 保存用に別インスタンスを使う（再生中でも保存可能にする）
            using var wavSynth = new SpeechSynthesizer();
            wavSynth.Rate   = _synth.Rate;
            wavSynth.Volume = _synth.Volume;

            // 同じ音声を選択（失敗しても続行）
            string currentVoice = CurrentVoiceName;
            if (!string.IsNullOrEmpty(currentVoice))
            {
                try { wavSynth.SelectVoice(currentVoice); }
                catch { /* 無視 */ }
            }

            wavSynth.SetOutputToWaveFile(outputPath);
            wavSynth.Speak(text);
            wavSynth.SetOutputToDefaultAudioDevice();

            Logger.Info($"WAV 保存完了: {outputPath}");
        }

        // ----------------------------------------------------------------
        // IDisposable
        // ----------------------------------------------------------------

        public void Dispose()
        {
            if (!_disposed)
            {
                _synth.SpeakAsyncCancelAll();
                _synth.Dispose();
                _disposed = true;
            }
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private void RaiseOnUiThread(Action action)
        {
            if (_uiContext != null)
                _uiContext.Post(_ => action(), null);
            else
                action();
        }
    }
}
