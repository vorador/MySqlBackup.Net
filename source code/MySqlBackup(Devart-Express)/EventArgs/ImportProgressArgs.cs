using System;
using System.Collections.Generic;
using System.Text;

namespace Devart.Data.MySql
{
    public class ImportProgressArgs : EventArgs
    {
        /// <summary>
        /// Number of processed bytes in current import process.
        /// </summary>
        public long CurrentBytes { get; } = 0L;

        /// <summary>
        /// Total bytes to be processed.
        /// </summary>
        public long TotalBytes { get; } = 0L;

        /// <summary>
        /// Percentage of completeness.
        /// </summary>
        public double PercentageCompleted { get; } = 0d;

        public ImportProgressArgs(long currentBytes, long totalBytes)
        {
            CurrentBytes = currentBytes;
            TotalBytes = totalBytes;

            if (currentBytes == 0L || totalBytes == 0L)
            {
                PercentageCompleted = 0d;
            }
            else
            {
                PercentageCompleted = (double)currentBytes / (double)totalBytes * 100d;
            }
        }
    }
}
