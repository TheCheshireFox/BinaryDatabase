using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Utility
{
    public class ProgressLogger : IDisposable
    {
        private readonly LoggerInterface logger;
        private readonly object sync = new object();
        private readonly string caption;

        private CancellationTokenSource cts;
        private decimal max = 100;
        private decimal current = 0;
        private Thread processTask;
        

        private void OnProgress(decimal val, TimeSpan est)
        {
            logger.LogInfo($"{(caption != null ? $"[{caption}] " : "")}Progress: {val.ToString("F2")}\t est: {est.ToString(@"hh\:mm\:ss")}");
        }

        private void OnUnknownMaxProgress()
        {
            Console.Write(".");
        }

        private const decimal SMOOTHING_FACTOR = 0.05M;
        private decimal CalcSpeed(decimal lastSpeed, decimal avgSpeed)
        {
            return SMOOTHING_FACTOR * lastSpeed + (1 - SMOOTHING_FACTOR) * avgSpeed;
        }

        private void ProcessQueue()
        {
            var lastLog = DateTime.UtcNow;
            var avgSpeed = 0M;
            decimal oldCurrent = 0;

            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(1000);

                if (max == 0)
                {
                    OnUnknownMaxProgress();
                    continue;
                }

                decimal cCurrent = 0;
                lock (sync)
                {
                    cCurrent = current;
                }

                if (cCurrent - oldCurrent < 0.01M) continue;

                avgSpeed = CalcSpeed((cCurrent - oldCurrent) / (decimal)(DateTime.UtcNow - lastLog).TotalSeconds, avgSpeed);

                OnProgress(current, TimeSpan.FromSeconds((double)((100 - current) / avgSpeed)));

                oldCurrent = current;
                lastLog = DateTime.UtcNow;
            }
        }

        public ProgressLogger(LoggerInterface logger, string caption = null)
        {
            this.logger = logger;
            this.caption = caption;
        }

        public void StartProgress(string progressMsg, decimal newMax)
        {
            logger.LogInfo(progressMsg);

            cts = new CancellationTokenSource();
            lock (sync)
            {
                max = newMax;
                current = 0;
            }
            processTask = new Thread(ProcessQueue);
            processTask.Start();
        }

        public void StopProgress(string completeMsg)
        {
            cts.Cancel();
            processTask.Join();
            if (current < 100M)
            {
                OnProgress(100M, TimeSpan.MinValue);
            }

            if (max == 0)
            {
                Console.WriteLine();
            }
            logger.LogInfo(completeMsg);
        }

        public void EmitIncrement(decimal p = 1)
        {
            lock (sync)
            {
                current += max != 0 ? p * 100M / max : 0;
            }
        }

        public void Dispose()
        {
            StopProgress("Interrupted");
        }
    }
}
