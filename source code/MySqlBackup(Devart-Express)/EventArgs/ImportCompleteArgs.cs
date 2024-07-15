using System;
using System.Collections.Generic;
using System.Text;

namespace Devart.Data.MySql
{
    public class ImportCompleteArgs
    {
        /// <summary>
        /// The starting time of import process.
        /// </summary>
        public DateTime TimeStart { get; }

        /// <summary>
        /// The ending time of import process.
        /// </summary>
        public DateTime TimeEnd { get; }

        /// <summary>
        /// The completion type of current import processs.
        /// </summary>
        public MySqlBackup.ProcessEndType CompleteType { get; }

        /// <summary>
        /// Indicates whether the import process has error(s).
        /// </summary>
        public bool HasErrors { get { if (LastError != null) return true; return false; } }

        /// <summary>
        /// The last error (exception) occur in import process.
        /// </summary>
        public Exception LastError { get; }

        /// <summary>
        /// Total time used in current import process.
        /// </summary>
        public TimeSpan TimeUsed { get; }

        public ImportCompleteArgs(MySqlBackup.ProcessEndType completionType, DateTime timeStart, DateTime timeEnd, Exception exception)
        {
            CompleteType = completionType;
            TimeStart = timeStart;
            TimeEnd = timeEnd;
            TimeUsed = timeEnd - timeStart;
            LastError = exception;
        }
    }
}
