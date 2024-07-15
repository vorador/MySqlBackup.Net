using System;

namespace MySqlConnector
{
    public class ExportProgressArgs : EventArgs
    {
        public string CurrentTableName { get; }
        public long TotalRowsInCurrentTable { get; } = 0;
        public long TotalRowsInAllTables { get; } = 0;
        public long CurrentRowIndexInCurrentTable { get; } = 0;
        public long CurrentRowIndexInAllTables { get; } = 0;
        public int TotalTables { get; } = 0;
        public int CurrentTableIndex { get; } = 0;

        public ExportProgressArgs(string currentTableName,
            long totalRowsInCurrentTable,
            long totalRowsInAllTables,
            long currentRowIndexInCurrentTable,
            long currentRowIndexInAllTable,
            int totalTables,
            int currentTableIndex)
        {
            CurrentTableName = currentTableName;
            TotalRowsInCurrentTable = totalRowsInCurrentTable;
            TotalRowsInAllTables = totalRowsInAllTables;
            CurrentRowIndexInCurrentTable = currentRowIndexInCurrentTable;
            CurrentRowIndexInAllTables = currentRowIndexInAllTable;
            TotalTables = totalTables;
            CurrentTableIndex = currentTableIndex;
        }
    }
}
