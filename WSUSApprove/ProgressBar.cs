using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WSUSApprove {
    public class ProgressData {
        public double _PercentCompleted;
        public int _CompletedCount;
        public int _TotalCount;
        public int _ConsoleWidth;
    }
    public class MyTaskProgressReport {
        public int CurrentProgressAmount { get; set; }
        public int TotalProgressAmount { get; set; }
        public string CurrentProgressMessage { get; set; }
    }
    public class ProgressBar : IDisposable, IProgress<ProgressData> {
        private const int blockCount = 50;
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0);
        private const string animation = @"|/-\";
        private readonly Timer timer;
        private double currentProgress = 0;
        private string currentText = string.Empty;
        private bool disposed = false;
        private DateTime _StartTime = DateTime.Now;
        private int _CompletedCount;
        private int _TotalCount;
        private int _ConsoleWidth;

        public ProgressBar(DateTime StartTime) {
            _StartTime = StartTime;
            timer = new Timer(TimerHandler);

            if (!Console.IsOutputRedirected) {
                ResetTimer();
            }
        }
        public void Report(ProgressData progress) {
            var value = Math.Max(0, Math.Min(1, progress._PercentCompleted));
            Interlocked.Exchange(ref currentProgress, value);
            Interlocked.Exchange(ref _CompletedCount, progress._CompletedCount);
            Interlocked.Exchange(ref _TotalCount, progress._TotalCount);
            Interlocked.Exchange(ref _ConsoleWidth, progress._ConsoleWidth);
        }
        private void TimerHandler(object state) {
            lock (timer) {
                if (disposed) return;

                int progressBlockCount = (int)(currentProgress * blockCount);
                double percent = (double)(currentProgress * 100);

                if (_CompletedCount > 0) {
                    DateTime Now = DateTime.Now;
                    TimeSpan ElapsedTime = Now.Subtract(_StartTime);
                    double AverageTimePerThread = ElapsedTime.TotalSeconds / _CompletedCount;
                    double EstimatedTotalSeconds = ElapsedTime.TotalSeconds / _CompletedCount * _TotalCount;
                    TimeSpan TotalEstimated = TimeSpan.FromSeconds(AverageTimePerThread * _TotalCount);
                    TimeSpan TotalEstimatedRemaining = TimeSpan.FromSeconds(EstimatedTotalSeconds - ElapsedTime.TotalSeconds);
                    string text = String.Empty;

                    if (_ConsoleWidth > 100 && _ConsoleWidth <= 120) {
                        text = string.Format("[{0}] [{1} of {2}][{3:N2}%] AVG: {4} seconds Elapsed: {5} ETA: {6}",
                            Now.ToString("T"), _CompletedCount, _TotalCount, percent, AverageTimePerThread.ToString("N3"), String.Format("{0:00}:{1:00}:{2:00}", (int)ElapsedTime.TotalHours, ElapsedTime.Minutes, ElapsedTime.Seconds), _StartTime.AddSeconds(EstimatedTotalSeconds).ToString("G"));
                    } else if (_ConsoleWidth >= 150) {
                        text = string.Format("[{0}] [{1} of {2}][{3:N2}%] AVG: {4} seconds Elapsed: {5} Estimated: {6} ({7}) ETA: {8}",
                            Now.ToString("T"), _CompletedCount, _TotalCount, percent, AverageTimePerThread.ToString("N3"), String.Format("{0:00}:{1:00}:{2:00}", (int)ElapsedTime.TotalHours, ElapsedTime.Minutes, ElapsedTime.Seconds), String.Format("{0:00}:{1:00}:{2:00}", (int)TotalEstimated.TotalHours, TotalEstimated.Minutes, TotalEstimated.Seconds), String.Format("{0:00}:{1:00}:{2:00}", (int)TotalEstimatedRemaining.TotalHours, TotalEstimatedRemaining.Minutes, TotalEstimatedRemaining.Seconds), _StartTime.AddSeconds(EstimatedTotalSeconds).ToString("G"));
                    } else {
                        text = string.Format("[{0}] [{1} of {2}][{3:N2}%] AVG: {4} seconds ETA: {5}",
                            Now.ToString("T"), _CompletedCount, _TotalCount, percent, AverageTimePerThread.ToString("N3"), _StartTime.AddSeconds(EstimatedTotalSeconds).ToString("G"));
                    }
                    UpdateText(text);
                    ResetTimer();
                } else {
                    DateTime Now = DateTime.Now;
                    TimeSpan ElapsedTime = Now.Subtract(_StartTime);
                    double average = ElapsedTime.TotalSeconds / _CompletedCount;
                    string text = string.Format("[{0}] [{1} of {2}][{3:N2}%] Elapsed: {4} Calculating ETA, Please Wait...", Now.ToString("T"), _CompletedCount, _TotalCount, percent, String.Format("{0:hh\\:mm\\:ss}", ElapsedTime));
                    UpdateText(text);
                    ResetTimer();
                }
            }
        }

        private void UpdateText(string text) {
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength]) {
                commonPrefixLength++;
            }

            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);
            outputBuilder.Append(text.Substring(commonPrefixLength));

            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0) {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }
            Console.Write(outputBuilder);
            currentText = text;
        }

        private void ResetTimer() {
            timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose() {
            lock (timer) {
                disposed = true;
                UpdateText(string.Empty);
            }
        }
    }
}
