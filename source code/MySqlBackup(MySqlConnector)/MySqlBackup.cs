using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;

namespace MySqlConnector
{
    public class MySqlBackup : IDisposable
    {
        private enum ProcessType
        {
            Export,
            Import
        }

        public enum ProcessEndType
        {
            UnknownStatus,
            Complete,
            Cancelled,
            Error
        }

        public static string Version =>
            typeof(MySqlBackup).Assembly.GetName().Version.ToString();

        private Encoding TextEncoding
        {
            get
            {
                try
                {
                    return ExportInfo.TextEncoding;
                }
                catch { /*Fallback on the next line*/ }
                return new UTF8Encoding(false);
            }
        }

        private TextWriter _textWriter;
        private TextReader _textReader;
        private DateTime _timeStart;
        private DateTime _timeEnd;
        private ProcessType _currentProcess;

        private ProcessEndType _processCompletionType;
        private bool _stopProcess = false;

        private string _currentTableName = string.Empty;
        private long _totalRowsInCurrentTable = 0;
        private long _totalRowsInAllTables = 0;
        private long _currentRowIndexInCurrentTable = 0;
        private long _currentRowIndexInAllTable = 0;
        private int _totalTables = 0;
        private int _currentTableIndex = 0;
        private Timer _timerReport = null;

        private long _currentBytes = 0L;
        private long _totalBytes = 0L;
        private StringBuilder _sbImport = null;
        private MySqlScript _mySqlScript = null;
        private string _delimiter = string.Empty;

        private enum NextImportAction
        {
            Ignore,
            SetNames,
            CreateNewDatabase,
            AppendLine,
            ChangeDelimiter,
            AppendLineAndExecute
        }

        public Exception LastError { get; private set; }

        public string LastErrorSql { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the information about the connected database.
        /// </summary>
        public MySqlDatabase Database { get; } = new();

        /// <summary>
        /// Gets the information about the connected MySQL server.
        /// </summary>
        public MySqlServer Server { get; set; } = new();

        /// <summary>
        /// Gets or Sets the instance of MySqlCommand.
        /// </summary>
        public MySqlCommand Command { get; set; }

        public readonly ExportInformations ExportInfo = new();
        public readonly ImportInformations ImportInfo = new();

        public delegate void ExportProgressChange(object sender, ExportProgressArgs e);
        public event ExportProgressChange ExportProgressChanged;

        public delegate void ExportComplete(object sender, ExportCompleteArgs e);
        public event ExportComplete ExportCompleted;

        public delegate void ImportProgressChange(object sender, ImportProgressArgs e);
        public event ImportProgressChange ImportProgressChanged;

        public delegate void ImportComplete(object sender, ImportCompleteArgs e);
        public event ImportComplete ImportCompleted;

        public delegate void GetTotalRowsProgressChange(object sender, GetTotalRowsArgs e);
        public event GetTotalRowsProgressChange GetTotalRowsProgressChanged;

        public MySqlBackup()
        {
            InitializeComponents();
        }

        public MySqlBackup(MySqlCommand cmd)
        {
            InitializeComponents();
            Command = cmd;
        }

        private void InitializeComponents()
        {
            Database.GetTotalRowsProgressChanged += DatabaseGetTotalRowsProgressChanged;

            _timerReport = new Timer();
            _timerReport.Elapsed += TimerReportElapsed;
        }

        private void DatabaseGetTotalRowsProgressChanged(object sender, GetTotalRowsArgs e)
        {
            GetTotalRowsProgressChanged?.Invoke(this, e);
        }

        #region Export

        public string ExportToString()
        {
            using var ms = new MemoryStream();
            ExportToMemoryStream(ms);
            ms.Position = 0L;
            
            using var thisReader = new StreamReader(ms);
            return thisReader.ReadToEnd();
        }

        public void ExportToFile(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (_textWriter = new StreamWriter(filePath, false, TextEncoding))
            {
                ExportStart();
                _textWriter.Close();
            }
        }

        public void ExportToTextWriter(TextWriter tw)
        {
            _textWriter = tw;
            ExportStart();
        }

        public void ExportToMemoryStream(MemoryStream ms, bool resetMemoryStreamPosition = true)
        {
            if (resetMemoryStreamPosition)
            {
                ms ??= new MemoryStream();
                
                if (ms.Length > 0)
                    ms = new MemoryStream();
                
                ms.Position = 0L;
            }

            _textWriter = new StreamWriter(ms, TextEncoding);
            ExportStart();
        }

        public void ExportToStream(Stream sm)
        {
            if (sm.CanSeek)
                sm.Seek(0, SeekOrigin.Begin);

            _textWriter = new StreamWriter(sm, TextEncoding);
            ExportStart();
        }

        private void ExportStart()
        {
            try
            {
                ExportInitializeVariables();

                int stage = 1;
                while (stage < 11)
                {
                    if (_stopProcess)
                        break;

                    switch (stage)
                    {
                        case 1: ExportBasicInfo(); break;
                        case 2: ExportCreateDatabase(); break;
                        case 3: ExportDocumentHeader(); break;
                        case 4: ExportTableRows(); break;
                        case 5: ExportFunctions(); break;
                        case 6: ExportProcedures(); break;
                        case 7: ExportEvents(); break;
                        case 8: ExportViews(); break;
                        case 9: ExportTriggers(); break;
                        case 10: ExportDocumentFooter(); break;
                        default: break;
                    }

                    _textWriter.Flush();

                    stage += 1;
                }

                _processCompletionType = _stopProcess ? ProcessEndType.Cancelled : ProcessEndType.Complete;
            }
            catch (Exception ex)
            {
                LastError = ex;
                StopAllProcess();
                throw;
            }
            finally
            {
                ReportEndProcess();
            }
        }

        private void ExportInitializeVariables()
        {
            if (Command == null)
                throw new Exception("MySqlCommand is not initialized. Object not set to an instance of an object.");

            if (Command.Connection == null)
                throw new Exception("MySqlCommand.Connection is not initialized. Object not set to an instance of an object.");

            if (Command.Connection.State != System.Data.ConnectionState.Open)
                throw new Exception("MySqlCommand.Connection is not opened.");

            if (ExportInfo.BlobExportMode == BlobDataExportMode.BinaryChar &&
                !ExportInfo.BlobExportModeForBinaryStringAllow)
                throw new Exception("[ExportInfo.BlobExportMode = BlobDataExportMode.BinaryString] is still under development. Please join the discussion at https://github.com/MySqlBackupNET/MySqlBackup.Net/issues (Title: Help requires. Unable to export BLOB in Char Format)");

            _timeStart = DateTime.Now;

            _stopProcess = false;
            _processCompletionType = ProcessEndType.UnknownStatus;
            _currentProcess = ProcessType.Export;
            LastError = null;
            _timerReport.Interval = ExportInfo.IntervalForProgressReport;

            Database.GetDatabaseInfo(Command, ExportInfo.GetTotalRowsMode);
            Server.GetServerInfo(Command);
            
            _currentTableName = string.Empty;
            _totalRowsInCurrentTable = 0L;
            _totalRowsInAllTables = ExportGetTablesToBeExported().Sum(pair => Database.Tables[pair.Key].TotalRows);
            _currentRowIndexInCurrentTable = 0;
            _currentRowIndexInAllTable = 0;
            _totalTables = 0;
            _currentTableIndex = 0;
        }

        private void ExportBasicInfo()
        {
            ExportWriteComment($"MySqlBackup.NET {Version}");

            ExportWriteComment(ExportInfo.RecordDumpTime
                ? $"Dump Time: {_timeStart:yyyy-MM-dd HH:mm:ss}"
                : string.Empty);

            ExportWriteComment("--------------------------------------");
            ExportWriteComment($"Server version {Server.Version}");
            _textWriter.WriteLine();
        }

        private void ExportCreateDatabase()
        {
            if (!ExportInfo.AddCreateDatabase && !ExportInfo.AddDropDatabase)
                return;

            _textWriter.WriteLine();
            _textWriter.WriteLine();
            
            if (ExportInfo.AddDropDatabase)
                ExportWriteLine($"DROP DATABASE `{Database.Name}`;");
            
            if (ExportInfo.AddCreateDatabase)
            {
                ExportWriteLine(Database.CreateDatabaseSql);
                ExportWriteLine($"USE `{Database.Name}`;");
            }
            
            _textWriter.WriteLine();
            _textWriter.WriteLine();
        }

        private void ExportDocumentHeader()
        {
            _textWriter.WriteLine();

            List<string> lstHeaders = ExportInfo.GetDocumentHeaders(Command);
            
            if (lstHeaders.Count <= 0) 
                return;
            
            foreach (string s in lstHeaders)
                ExportWriteLine(s);

            _textWriter.WriteLine();
            _textWriter.WriteLine();
        }

        private void ExportTableRows()
        {
            Dictionary<string, string> dicTables = ExportGetTablesToBeExportedReArranged();

            _totalTables = dicTables.Count;

            if (!ExportInfo.ExportTableStructure && !ExportInfo.ExportRows)
                return;
            
            if (ExportProgressChanged != null)
                _timerReport.Start();

            foreach (KeyValuePair<string, string> kvTable in dicTables)
            {
                if (_stopProcess)
                    return;

                string tableName = kvTable.Key;
                string selectSql = kvTable.Value;

                bool exclude = ExportThisTableIsExcluded(tableName);
                
                if (exclude)
                    continue;

                _currentTableName = tableName;
                _currentTableIndex += 1;
                _totalRowsInCurrentTable = Database.Tables[tableName].TotalRows;

                if (ExportInfo.ExportTableStructure)
                    ExportTableStructure(tableName);

                if (ExportInfo.ExportRows)
                    ExportRows(tableName, selectSql);
            }
        }

        private bool ExportThisTableIsExcluded(string tableName)
        {
            string tableNameLower = tableName.ToLower();

            foreach (string blacklistedTable in ExportInfo.ExcludeTables)
                if (blacklistedTable.ToLower() == tableNameLower)
                    return true;

            return false;
        }

        private void ExportTableStructure(string tableName)
        {
            if (_stopProcess)
                return;

            ExportWriteComment(string.Empty);
            ExportWriteComment($"Definition of {tableName}");
            ExportWriteComment(string.Empty);

            _textWriter.WriteLine();

            if (ExportInfo.AddDropTable)
                ExportWriteLine($"DROP TABLE IF EXISTS `{tableName}`;");

            ExportWriteLine(ExportInfo.ResetAutoIncrement
                ? Database.Tables[tableName].CreateTableSqlWithoutAutoIncrement
                : Database.Tables[tableName].CreateTableSql);

            _textWriter.WriteLine();
            _textWriter.Flush();
        }

        private Dictionary<string, string> ExportGetTablesToBeExportedReArranged()
        {
            Dictionary<string, string> tableSelectQueries = ExportGetTablesToBeExported();
            var tableCreateQueries = new Dictionary<string, string>();
            
            foreach (KeyValuePair<string, string> selectPair in tableSelectQueries)
                tableCreateQueries[selectPair.Key] = Database.Tables[selectPair.Key].CreateTableSql;

            List<string> lst = ExportReArrangeDependencies(tableCreateQueries, "foreign key", "`");
            tableCreateQueries = lst.ToDictionary(k => k, k => tableSelectQueries[k]);
            return tableCreateQueries;
        }

        private Dictionary<string, string> ExportGetTablesToBeExported()
        {
            if (ExportInfo.TablesToBeExportedDic is null || ExportInfo.TablesToBeExportedDic.Count == 0)
                return Database.Tables
                    .ToDictionary(
                        table => table.Name,
                        table => $"SELECT * FROM `{table.Name}`;");

            return ExportInfo.TablesToBeExportedDic
                .Where(table => Database.Tables.Contains(table.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private List<string> ExportReArrangeDependencies(Dictionary<string, string> tableCreateQueries, string splitKeyword, string keyNameWrapper)
        {
            var lst = new List<string>();
            var index = new HashSet<string>();
            var requireLoop = true;

            while (requireLoop)
            {
                requireLoop = false;

                foreach (KeyValuePair<string, string> createPair in tableCreateQueries)
                {
                    if (index.Contains(createPair.Key))
                        continue;

                    var allReferencedAdded = true;
                    string createSql = createPair.Value.ToLower();
                    var referenceInfo = string.Empty;

                    bool referenceTaken = false;
                    if (!string.IsNullOrEmpty(splitKeyword))
                    {
                        if (createSql.Contains($" {splitKeyword} "))
                        {
                            string[] sa = createSql.Split(new[] { $" {splitKeyword} " }, StringSplitOptions.RemoveEmptyEntries);
                            referenceInfo = sa[sa.Length - 1];
                            referenceTaken = true;
                        }
                    }

                    if (!referenceTaken)
                        referenceInfo = createSql;

                    foreach (KeyValuePair<string, string> localCreatePair in tableCreateQueries)
                    {
                        if (createPair.Key == localCreatePair.Key)
                            continue;

                        if (index.Contains(localCreatePair.Key))
                            continue;

                        string thisTBname = string.Format("{0}{1}{0}", keyNameWrapper, localCreatePair.Key.ToLower());

                        if (!referenceInfo.Contains(thisTBname))
                            continue;
                        
                        allReferencedAdded = false;
                        break;
                    }

                    if (!allReferencedAdded) 
                        continue;
                    
                    if (index.Contains(createPair.Key))
                        continue;
                    
                    lst.Add(createPair.Key);
                    index.Add(createPair.Key);
                    requireLoop = true;
                    break;
                }
            }

            foreach (KeyValuePair<string, string> kv in tableCreateQueries)
            {
                if (index.Contains(kv.Key))
                    continue;
                
                lst.Add(kv.Key);
                index.Add(kv.Key);
            }

            return lst;
        }

        private void ExportRows(string tableName, string selectSql)
        {
            ExportWriteComment(string.Empty);
            ExportWriteComment($"Dumping data for table {tableName}");
            ExportWriteComment(string.Empty);
            _textWriter.WriteLine();
            ExportWriteLine($"/*!40000 ALTER TABLE `{tableName}` DISABLE KEYS */;");

            if (ExportInfo.WrapWithinTransaction)
                ExportWriteLine("START TRANSACTION;");

            ExportRowsData(tableName, selectSql);

            if (ExportInfo.WrapWithinTransaction)
                ExportWriteLine("COMMIT;");

            ExportWriteLine($"/*!40000 ALTER TABLE `{tableName}` ENABLE KEYS */;");
            _textWriter.WriteLine();
            _textWriter.Flush();
        }

        private void ExportRowsData(string tableName, string selectSql)
        {
            _currentRowIndexInCurrentTable = 0L;

            switch (ExportInfo.RowsExportMode)
            {
                case RowsDataExportMode.Insert:
                case RowsDataExportMode.InsertIgnore:
                case RowsDataExportMode.Replace:
                    ExportRowsDataInsertIgnoreReplace(tableName, selectSql);
                    break;
                case RowsDataExportMode.OnDuplicateKeyUpdate:
                    ExportRowsDataOnDuplicateKeyUpdate(tableName, selectSql);
                    break;
                case RowsDataExportMode.Update:
                    ExportRowsDataUpdate(tableName, selectSql);
                    break;
            }
        }

        private void ExportRowsDataInsertIgnoreReplace(string tableName, string selectSql)
        {
            MySqlTable table = Database.Tables[tableName];

            Command.CommandText = selectSql;
            MySqlDataReader rdr = Command.ExecuteReader();

            string insertStatementHeader = null;

            var sb = new StringBuilder(ExportInfo.MaxSqlLength);

            while (rdr.Read())
            {
                if (_stopProcess)
                    return;

                _currentRowIndexInAllTable += 1;
                _currentRowIndexInCurrentTable += 1;

                insertStatementHeader ??= ExportGetInsertStatementHeader(ExportInfo.RowsExportMode, tableName, rdr);

                string sqlDataRow = ExportGetValueString(rdr, table);

                if (sb.Length == 0)
                {
                    if (ExportInfo.InsertLineBreakBetweenInserts)
                        sb.AppendLine(insertStatementHeader);
                    else
                        sb.Append(insertStatementHeader);
                    
                    sb.Append(sqlDataRow);
                }
                else if (sb.Length + (long)sqlDataRow.Length < ExportInfo.MaxSqlLength)
                {
                    if (ExportInfo.InsertLineBreakBetweenInserts)
                        sb.AppendLine(",");
                    else
                        sb.Append(",");
                    
                    sb.Append(sqlDataRow);
                }
                else
                {
                    sb.AppendFormat(";");

                    ExportWriteLine(sb.ToString());
                    _textWriter.Flush();

                    sb.Clear();
                    sb.AppendLine(insertStatementHeader);
                    sb.Append(sqlDataRow);
                }
            }

            rdr.Close();

            if (sb.Length > 0)
                sb.Append(";");

            ExportWriteLine(sb.ToString());
            _textWriter.Flush();
            sb.Clear();
        }

        private void ExportRowsDataOnDuplicateKeyUpdate(string tableName, string selectSql)
        {
            MySqlTable table = Database.Tables[tableName];
            bool allPrimaryField = table.Columns.All(col => col.IsPrimaryKey);

            Command.CommandText = selectSql;
            MySqlDataReader rdr = Command.ExecuteReader();

            while (rdr.Read())
            {
                if (_stopProcess)
                    return;

                _currentRowIndexInAllTable += 1;
                _currentRowIndexInCurrentTable += 1;

                var sb = new StringBuilder();

                if (allPrimaryField)
                {
                    sb.Append(ExportGetInsertStatementHeader(RowsDataExportMode.InsertIgnore, tableName, rdr));
                    sb.Append(ExportGetValueString(rdr, table));
                }
                else
                {
                    sb.Append(ExportGetInsertStatementHeader(RowsDataExportMode.Insert, tableName, rdr));
                    sb.Append(ExportGetValueString(rdr, table));
                    sb.Append(" ON DUPLICATE KEY UPDATE ");
                    ExportGetUpdateString(rdr, table, sb);
                }

                sb.Append(";");

                ExportWriteLine(sb.ToString());
                _textWriter.Flush();
                sb.Clear();
            }

            rdr.Close();
        }

        private void ExportRowsDataUpdate(string tableName, string selectSql)
        {
            MySqlTable table = Database.Tables[tableName];
            bool allPrimaryField = table.Columns.All(col => col.IsPrimaryKey);

            if (allPrimaryField)
                return;

            bool allNonPrimaryField = table.Columns.All(col => !col.IsPrimaryKey);

            if (allNonPrimaryField)
                return;

            Command.CommandText = selectSql;
            MySqlDataReader rdr = Command.ExecuteReader();

            while (rdr.Read())
            {
                if (_stopProcess)
                    return;

                _currentRowIndexInAllTable += 1;
                _currentRowIndexInCurrentTable += 1;

                var sb = new StringBuilder();
                sb.Append("UPDATE `");
                sb.Append(tableName);
                sb.Append("` SET ");

                ExportGetUpdateString(rdr, table, sb);

                sb.Append(" WHERE ");

                ExportGetConditionString(rdr, table, sb);

                sb.Append(";");

                ExportWriteLine(sb.ToString());

                _textWriter.Flush();
                sb.Clear();
            }

            rdr.Close();
        }

        private string ExportGetInsertStatementHeader(RowsDataExportMode rowsExportMode, string tableName, MySqlDataReader rdr)
        {
            var sb = new StringBuilder();

            switch (rowsExportMode)
            {
                case RowsDataExportMode.Insert:
                    sb.Append("INSERT INTO `");
                    break;
                case RowsDataExportMode.InsertIgnore:
                    sb.Append("INSERT IGNORE INTO `");
                    break;
                case RowsDataExportMode.Replace:
                    sb.Append("REPLACE INTO `");
                    break;
            }

            sb.Append(tableName);
            sb.Append("`(");

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string colname = rdr.GetName(i);

                if (Database.Tables[tableName].Columns[colname].IsGeneratedColumn)
                    continue;

                if (i > 0)
                    sb.Append(",");
                
                sb.Append("`");
                sb.Append(rdr.GetName(i));
                sb.Append("`");
            }

            sb.Append(") VALUES");
            return sb.ToString();
        }

        private string ExportGetValueString(MySqlDataReader rdr, MySqlTable table)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string columnName = rdr.GetName(i);

                if (table.Columns[columnName].IsGeneratedColumn)
                    continue;

                sb.AppendFormat(sb.Length == 0 ? "(" : ",");
                
                object ob = rdr[i];
                MySqlColumn col = table.Columns[columnName];

                sb.Append(QueryExpress.ConvertToSqlFormat(ob, true, true, col, ExportInfo.BlobExportMode));
            }

            sb.AppendFormat(")");
            return sb.ToString();
        }

        private void ExportGetUpdateString(MySqlDataReader rdr, MySqlTable table, StringBuilder sb)
        {
            bool isFirst = true;

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string colName = rdr.GetName(i);

                MySqlColumn col = table.Columns[colName];

                if (col.IsPrimaryKey) 
                    continue;
                
                if (isFirst)
                    isFirst = false;
                else
                    sb.Append(",");

                sb.Append("`");
                sb.Append(colName);
                sb.Append("`=");
                sb.Append(QueryExpress.ConvertToSqlFormat(rdr[i], true, true, col, ExportInfo.BlobExportMode));
            }
        }

        private void ExportGetConditionString(MySqlDataReader rdr, MySqlTable table, StringBuilder sb)
        {
            bool isFirst = true;

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string colName = rdr.GetName(i);
                MySqlColumn col = table.Columns[colName];

                if (!col.IsPrimaryKey) 
                    continue;
                
                if (isFirst)
                    isFirst = false;
                else
                    sb.Append(" and ");

                sb.Append("`");
                sb.Append(colName);
                sb.Append("`=");
                sb.Append(QueryExpress.ConvertToSqlFormat(rdr[i], true, true, col, ExportInfo.BlobExportMode));
            }
        }

        private void ExportProcedures()
        {
            if (!ExportInfo.ExportProcedures || Database.Procedures.Count == 0)
                return;

            ExportWriteComment(string.Empty);
            ExportWriteComment("Dumping procedures");
            ExportWriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlProcedure procedure in Database.Procedures)
            {
                if (_stopProcess)
                    return;

                if (procedure.CreateProcedureSqlWithoutDefiner.Trim().Length == 0 ||
                    procedure.CreateProcedureSql.Trim().Length == 0)
                    continue;

                ExportWriteLine($"DROP PROCEDURE IF EXISTS `{procedure.Name}`;");
                ExportWriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    ExportWriteLine(procedure.CreateProcedureSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    ExportWriteLine(procedure.CreateProcedureSql + " " + ExportInfo.ScriptsDelimiter);

                ExportWriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }
            _textWriter.Flush();
        }

        private void ExportFunctions()
        {
            if (!ExportInfo.ExportFunctions || Database.Functions.Count == 0)
                return;

            ExportWriteComment(string.Empty);
            ExportWriteComment("Dumping functions");
            ExportWriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlFunction function in Database.Functions)
            {
                if (_stopProcess)
                    return;

                if (function.CreateFunctionSql.Trim().Length == 0 ||
                    function.CreateFunctionSqlWithoutDefiner.Trim().Length == 0)
                    continue;

                ExportWriteLine($"DROP FUNCTION IF EXISTS `{function.Name}`;");
                ExportWriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    ExportWriteLine(function.CreateFunctionSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    ExportWriteLine(function.CreateFunctionSql + " " + ExportInfo.ScriptsDelimiter);

                ExportWriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }

            _textWriter.Flush();
        }

        private void ExportViews()
        {
            if (!ExportInfo.ExportViews || Database.Views.Count == 0)
                return;

            // ReArrange Views
            var dicViewCreate = new Dictionary<string, string>();
            foreach (MySqlView view in Database.Views)
                dicViewCreate[view.Name] = view.CreateViewSql;

            List<string> lst = ExportReArrangeDependencies(dicViewCreate, null, "`");

            ExportWriteComment(string.Empty);
            ExportWriteComment("Dumping views");
            ExportWriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (string viewname in lst)
            {
                if (_stopProcess)
                    return;

                MySqlView view = Database.Views[viewname];

                if (view.CreateViewSql.Trim().Length == 0 || view.CreateViewSqlWithoutDefiner.Trim().Length == 0)
                    continue;

                ExportWriteLine($"DROP TABLE IF EXISTS `{view.Name}`;");
                ExportWriteLine($"DROP VIEW IF EXISTS `{view.Name}`;");

                ExportWriteLine(ExportInfo.ExportRoutinesWithoutDefiner
                    ? view.CreateViewSqlWithoutDefiner
                    : view.CreateViewSql);

                _textWriter.WriteLine();
            }

            _textWriter.WriteLine();
            _textWriter.Flush();
        }

        private void ExportEvents()
        {
            if (!ExportInfo.ExportEvents || Database.Events.Count == 0)
                return;

            ExportWriteComment(string.Empty);
            ExportWriteComment("Dumping events");
            ExportWriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlEvent e in Database.Events)
            {
                if (_stopProcess)
                    return;

                if (e.CreateEventSql.Trim().Length == 0 || e.CreateEventSqlWithoutDefiner.Trim().Length == 0)
                    continue;

                ExportWriteLine($"DROP EVENT IF EXISTS `{e.Name}`;");
                ExportWriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    ExportWriteLine(e.CreateEventSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    ExportWriteLine(e.CreateEventSql + " " + ExportInfo.ScriptsDelimiter);

                ExportWriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }

            _textWriter.Flush();
        }

        private void ExportTriggers()
        {
            if (!ExportInfo.ExportTriggers || Database.Triggers.Count == 0)
                return;

            ExportWriteComment(string.Empty);
            ExportWriteComment("Dumping triggers");
            ExportWriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlTrigger trigger in Database.Triggers)
            {
                if (_stopProcess)
                    return;

                string createTriggerSql = trigger.CreateTriggerSql.Trim();
                string createTriggerSqlWithoutDefiner = trigger.CreateTriggerSqlWithoutDefiner.Trim();
                
                if (createTriggerSql.Length == 0 ||
                    createTriggerSqlWithoutDefiner.Length == 0)
                    continue;

                ExportWriteLine($"DROP TRIGGER /*!50030 IF EXISTS */ `{trigger.Name}`;");
                ExportWriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    ExportWriteLine(createTriggerSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    ExportWriteLine(createTriggerSql + " " + ExportInfo.ScriptsDelimiter);

                ExportWriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }

            _textWriter.Flush();
        }

        private void ExportDocumentFooter()
        {
            _textWriter.WriteLine();

            List<string> lstFooters = ExportInfo.GetDocumentFooters();
            if (lstFooters.Count > 0)
                foreach (string s in lstFooters)
                    ExportWriteLine(s);

            _timeEnd = DateTime.Now;

            if (ExportInfo.RecordDumpTime)
            {
                TimeSpan ts = _timeEnd - _timeStart;

                _textWriter.WriteLine();
                _textWriter.WriteLine();
                ExportWriteComment($"Dump completed on {_timeEnd:yyyy-MM-dd HH:mm:ss}");
                ExportWriteComment(
                    $"Total time: {ts.Days}:{ts.Hours}:{ts.Minutes}:{ts.Seconds}:{ts.Milliseconds} (d:h:m:s:ms)");
            }

            _textWriter.Flush();
        }

        private void ExportWriteComment(string text)
        {
            if (ExportInfo.EnableComment)
                ExportWriteLine($"-- {text}");
        }

        private void ExportWriteLine(string text)
        {
            _textWriter.WriteLine(text);
        }

        #endregion

        #region Import

        public void ImportFromString(string sqldumptext)
        {
            using var ms = new MemoryStream();
            
            using var thisWriter = new StreamWriter(ms);
            thisWriter.Write(sqldumptext);
            thisWriter.Flush();

            ms.Position = 0L;

            ImportFromMemoryStream(ms);
        }

        public void ImportFromFile(string filePath)
        {
            var fi = new FileInfo(filePath);

            using TextReader tr = new StreamReader(filePath);
            ImportFromTextReaderStream(tr, fi);
        }

        public void ImportFromTextReader(TextReader tr)
        {
            ImportFromTextReaderStream(tr, null);
        }

        public void ImportFromMemoryStream(MemoryStream ms)
        {
            ms.Position = 0;
            _totalBytes = ms.Length;
            _textReader = new StreamReader(ms);
            ImportStart();
        }

        public void ImportFromStream(Stream sm)
        {
            if (sm.CanSeek)
                sm.Seek(0, SeekOrigin.Begin);

            _textReader = new StreamReader(sm);
            ImportStart();
        }

        private void ImportFromTextReaderStream(TextReader tr, FileInfo fileInfo)
        {
            _totalBytes = fileInfo?.Length ?? 0L;
            _textReader = tr;
            ImportStart();
        }

        private void ImportStart()
        {
            ImportInitializeVariables();

            try
            {
                var line = string.Empty;

                while (line != null)
                {
                    if (_stopProcess)
                    {
                        _processCompletionType = ProcessEndType.Cancelled;
                        break;
                    }

                    try
                    {
                        line = ImportGetLine();

                        if (line == null)
                            break;

                        if (line.Length == 0)
                            continue;

                        ImportProcessLine(line);
                    }
                    catch (Exception ex)
                    {
                        line = string.Empty;
                        LastError = ex;
                        LastErrorSql = _sbImport.ToString();

                        if (!string.IsNullOrEmpty(ImportInfo.ErrorLogFile))
                            File.AppendAllText(ImportInfo.ErrorLogFile, ex.Message + Environment.NewLine + Environment.NewLine + LastErrorSql + Environment.NewLine + Environment.NewLine);

                        _sbImport.Clear();

                        if (ImportInfo.IgnoreSqlError) 
                            continue;
                        
                        StopAllProcess();
                        throw;
                    }
                }
            }
            finally
            {
                ReportEndProcess();
            }
        }

        private void ImportInitializeVariables()
        {
            if (Command == null)
                throw new Exception("MySqlCommand is not initialized. Object not set to an instance of an object.");

            if (Command.Connection == null)
                throw new Exception("MySqlCommand.Connection is not initialized. Object not set to an instance of an object.");

            if (Command.Connection.State != System.Data.ConnectionState.Open)
                throw new Exception("MySqlCommand.Connection is not opened.");
            
            _stopProcess = false;
            LastError = null;
            _timeStart = DateTime.Now;
            _currentBytes = 0L;
            _sbImport = new StringBuilder();
            _mySqlScript = new MySqlScript(Command.Connection);
            _currentProcess = ProcessType.Import;
            _processCompletionType = ProcessEndType.Complete;
            _delimiter = ";";
            LastErrorSql = string.Empty;

            if (ImportProgressChanged != null)
                _timerReport.Start();
        }

        private string ImportGetLine()
        {
            string line = _textReader.ReadLine();

            if (line == null)
                return null;

            if (ImportProgressChanged != null)
                _currentBytes += line.Length;

            line = line.Trim();
            return ImportIsEmptyLine(line) ? string.Empty : line;
        }

        private void ImportProcessLine(string line)
        {
            NextImportAction nextAction = ImportAnalyseNextAction(line);

            switch (nextAction)
            {
                case NextImportAction.Ignore:
                    break;
                case NextImportAction.AppendLine:
                    ImportAppendLine(line);
                    break;
                case NextImportAction.ChangeDelimiter:
                    ImportChangeDelimiter(line);
                    ImportAppendLine(line);
                    break;
                case NextImportAction.AppendLineAndExecute:
                    ImportAppendLineAndExecute(line);
                    break;
                default:
                    break;
            }
        }

        private NextImportAction ImportAnalyseNextAction(string line)
        {
            if (line == null)
                return NextImportAction.Ignore;

            if (line == string.Empty)
                return NextImportAction.Ignore;

            if (line.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
                return NextImportAction.ChangeDelimiter;

            if (line.EndsWith(_delimiter))
                return NextImportAction.AppendLineAndExecute;

            return NextImportAction.AppendLine;
        }

        private void ImportAppendLine(string line)
        {
            _sbImport.AppendLine(line);
        }

        private void ImportChangeDelimiter(string line)
        {
            string nextDelimiter = line.Substring(9);
            _delimiter = nextDelimiter.Replace(" ", string.Empty);
        }

        private void ImportAppendLineAndExecute(string line)
        {
            _sbImport.Append(line);

            var query = _sbImport.ToString();

            if (query.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
            {
                _mySqlScript.Query = _sbImport.ToString();
                _mySqlScript.Delimiter = _delimiter;
                _mySqlScript.Execute();
            }
            else
            {
                Command.CommandText = query;
                Command.ExecuteNonQuery();
            }

            _sbImport.Clear();
        }

        private bool ImportIsEmptyLine(string line)
        {
            if (line == null)
                return true;
            if (line == string.Empty)
                return true;
            if (line.Length == 0)
                return true;
            if (line.StartsWith("--"))
                return true;
            if (line == Environment.NewLine)
                return true;
            if (line == "\r")
                return true;
            if (line == "\n")
                return true;
            if (line == "\r\n")
                return true;

            return false;
        }

        #endregion

        private void ReportEndProcess()
        {
            _timeEnd = DateTime.Now;

            StopAllProcess();

            switch (_currentProcess)
            {
                case ProcessType.Export:
                {
                    ReportProgress();
                    if (ExportCompleted != null)
                    {
                        var arg = new ExportCompleteArgs(_timeStart, _timeEnd, _processCompletionType, LastError);
                        ExportCompleted(this, arg);
                    }

                    break;
                }
                case ProcessType.Import:
                {
                    _currentBytes = _totalBytes;

                    ReportProgress();
                    if (ImportCompleted != null)
                    {
                        var completedType = ProcessEndType.UnknownStatus;
                        switch (_processCompletionType)
                        {
                            case ProcessEndType.Complete:
                                completedType = ProcessEndType.Complete;
                                break;
                            case ProcessEndType.Error:
                                completedType = ProcessEndType.Error;
                                break;
                            case ProcessEndType.Cancelled:
                                completedType = ProcessEndType.Cancelled;
                                break;
                        }

                        var arg = new ImportCompleteArgs(completedType, _timeStart, _timeEnd, LastError);
                        ImportCompleted(this, arg);
                    }

                    break;
                }
            }
        }

        private void TimerReportElapsed(object sender, ElapsedEventArgs e)
        {
            ReportProgress();
        }

        private void ReportProgress()
        {
            switch (_currentProcess)
            {
                case ProcessType.Export when ExportProgressChanged == null:
                    return;
                case ProcessType.Export:
                {
                    var arg = new ExportProgressArgs(_currentTableName, _totalRowsInCurrentTable, _totalRowsInAllTables, _currentRowIndexInCurrentTable, _currentRowIndexInAllTable, _totalTables, _currentTableIndex);
                    ExportProgressChanged(this, arg);
                    break;
                }
                case ProcessType.Import:
                {
                    if (ImportProgressChanged != null)
                    {
                        var arg = new ImportProgressArgs(_currentBytes, _totalBytes);
                        ImportProgressChanged(this, arg);
                    }

                    break;
                }
            }
        }

        public void StopAllProcess()
        {
            _stopProcess = true;
            _timerReport.Stop();
        }

        public void Dispose()
        {
            try
            {
                Database.Dispose();
            }
            catch { }

            try
            {
                Server = null;
            }
            catch { }

            try
            {
                _mySqlScript = null;
            }
            catch { }
        }
    }
}
