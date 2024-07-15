using System;
using System.Collections.Generic;
using System.Text;

namespace Devart.Data.MySql
{
    public class ExportCompleteArgs
    {
        /// <summary>
        /// The Starting time of export process.
        /// </summary>
        public DateTime TimeStart { get; }

        /// <summary>
        /// The Ending time of export process.
        /// </summary>
        public DateTime TimeEnd { get; }

        /// <summary>
        /// Total time used in current export process.
        /// </summary>
        public TimeSpan TimeUsed { get; }

        public MySqlBackup.ProcessEndType CompletionType { get; }

        public Exception LastError { get; }

        public bool HasError { get { if (LastError != null) return true; return false; } }

        public ExportCompleteArgs(DateTime timeStart, DateTime timeEnd, MySqlBackup.ProcessEndType endType, Exception exception)
        {
            CompletionType = endType;
            TimeStart = timeStart;
            TimeEnd = timeEnd;
            TimeUsed = timeEnd - timeStart;
            LastError = exception;
        }
    }
}
