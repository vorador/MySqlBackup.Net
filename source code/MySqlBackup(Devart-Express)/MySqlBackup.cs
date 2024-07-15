using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;

namespace Devart.Data.MySql
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
                catch { }
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

        public Exception LastError { get; private set; } = null;
        public string LastErrorSql { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the information about the connected database.
        /// </summary>
        public MySqlDatabase Database { get; } = new MySqlDatabase();

        /// <summary>
        /// Gets the information about the connected MySQL server.
        /// </summary>
        public MySqlServer Server { get; private set; } = new MySqlServer();

        /// <summary>
        /// Gets or Sets the instance of MySqlCommand.
        /// </summary>
        public MySqlCommand Command { get; set; }

        public ExportInformations ExportInfo = new ExportInformations();
        public ImportInformations ImportInfo = new ImportInformations();

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
            //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Database.GetTotalRowsProgressChanged += _database_GetTotalRowsProgressChanged;

            _timerReport = new Timer();
            _timerReport.Elapsed += timerReport_Elapsed;

            //textEncoding = new UTF8Encoding(false);
        }

        private void _database_GetTotalRowsProgressChanged(object sender, GetTotalRowsArgs e)
        {
            if (GetTotalRowsProgressChanged != null)
            {
                GetTotalRowsProgressChanged(this, e);
            }
        }

        #region Export

        public string ExportToString()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ExportToMemoryStream(ms);
                ms.Position = 0L;
                using (var thisReader = new StreamReader(ms))
                {
                    return thisReader.ReadToEnd();
                }
            }
        }

        public void ExportToFile(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

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

        public void ExportToMemoryStream(MemoryStream ms)
        {
            ExportToMemoryStream(ms, true);
        }

        public void ExportToMemoryStream(MemoryStream ms, bool resetMemoryStreamPosition)
        {
            if (resetMemoryStreamPosition)
            {
                if (ms == null)
                    ms = new MemoryStream();
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
                Export_InitializeVariables();

                int stage = 1;

                while (stage < 11)
                {
                    if (_stopProcess) break;

                    switch (stage)
                    {
                        case 1: Export_BasicInfo(); break;
                        case 2: Export_CreateDatabase(); break;
                        case 3: Export_DocumentHeader(); break;
                        case 4: Export_TableRows(); break;
                        case 5: Export_Functions(); break;
                        case 6: Export_Procedures(); break;
                        case 7: Export_Events(); break;
                        case 8: Export_Views(); break;
                        case 9: Export_Triggers(); break;
                        case 10: Export_DocumentFooter(); break;
                        default: break;
                    }

                    _textWriter.Flush();

                    stage = stage + 1;
                }

                if (_stopProcess) _processCompletionType = ProcessEndType.Cancelled;
                else _processCompletionType = ProcessEndType.Complete;
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

        private void Export_InitializeVariables()
        {
            if (Command == null)
            {
                throw new Exception("MySqlCommand is not initialized. Object not set to an instance of an object.");
            }

            if (Command.Connection == null)
            {
                throw new Exception("MySqlCommand.Connection is not initialized. Object not set to an instance of an object.");
            }

            if (Command.Connection.State != System.Data.ConnectionState.Open)
            {
                throw new Exception("MySqlCommand.Connection is not opened.");
            }

            if (ExportInfo.BlobExportMode == BlobDataExportMode.BinaryChar &&
                !ExportInfo.BlobExportModeForBinaryStringAllow)
            {
                throw new Exception("[ExportInfo.BlobExportMode = BlobDataExportMode.BinaryString] is still under development. Please join the discussion at https://github.com/MySqlBackupNET/MySqlBackup.Net/issues (Title: Help requires. Unable to export BLOB in Char Format)");
            }

            _timeStart = DateTime.Now;

            _stopProcess = false;
            _processCompletionType = ProcessEndType.UnknownStatus;
            _currentProcess = ProcessType.Export;
            LastError = null;
            _timerReport.Interval = ExportInfo.IntervalForProgressReport;
            //GetSHA512HashFromPassword(ExportInfo.EncryptionPassword);

            Database.GetDatabaseInfo(Command, ExportInfo.GetTotalRowsMode);
            Server.GetServerInfo(Command);
            _currentTableName = string.Empty;
            _totalRowsInCurrentTable = 0L;
            _totalRowsInAllTables = Export_GetTablesToBeExported()
                .Sum(pair => Database.Tables[pair.Key].TotalRows);
            _currentRowIndexInCurrentTable = 0;
            _currentRowIndexInAllTable = 0;
            _totalTables = 0;
            _currentTableIndex = 0;
        }

        private void Export_BasicInfo()
        {
            Export_WriteComment(string.Format("MySqlBackup.NET {0}", Version));

            if (ExportInfo.RecordDumpTime)
                Export_WriteComment(string.Format("Dump Time: {0}", _timeStart.ToString("yyyy-MM-dd HH:mm:ss")));
            else
                Export_WriteComment(string.Empty);

            Export_WriteComment("--------------------------------------");
            Export_WriteComment(string.Format("Server version {0}", Server.Version));
            _textWriter.WriteLine();
        }

        private void Export_CreateDatabase()
        {
            if (!ExportInfo.AddCreateDatabase && !ExportInfo.AddDropDatabase)
                return;

            _textWriter.WriteLine();
            _textWriter.WriteLine();
            if (ExportInfo.AddDropDatabase)
                Export_WriteLine(String.Format("DROP DATABASE `{0}`;", Database.Name));
            if (ExportInfo.AddCreateDatabase)
            {
                Export_WriteLine(Database.CreateDatabaseSql);
                Export_WriteLine(string.Format("USE `{0}`;", Database.Name));
            }
            _textWriter.WriteLine();
            _textWriter.WriteLine();
        }

        private void Export_DocumentHeader()
        {
            _textWriter.WriteLine();

            List<string> lstHeaders = ExportInfo.GetDocumentHeaders(Command);
            if (lstHeaders.Count > 0)
            {
                foreach (string s in lstHeaders)
                {
                    Export_WriteLine(s);
                }

                _textWriter.WriteLine();
                _textWriter.WriteLine();
            }
        }

        private void Export_TableRows()
        {
            Dictionary<string, string> dicTables = Export_GetTablesToBeExportedReArranged();

            _totalTables = dicTables.Count;

            if (ExportInfo.ExportTableStructure || ExportInfo.ExportRows)
            {
                if (ExportProgressChanged != null)
                    _timerReport.Start();

                foreach (KeyValuePair<string, string> kvTable in dicTables)
                {
                    if (_stopProcess)
                        return;

                    string tableName = kvTable.Key;
                    string selectSql = kvTable.Value;

                    bool exclude = Export_ThisTableIsExcluded(tableName);
                    if (exclude)
                    {
                        continue;
                    }

                    _currentTableName = tableName;
                    _currentTableIndex = _currentTableIndex + 1;
                    _totalRowsInCurrentTable = Database.Tables[tableName].TotalRows;

                    if (ExportInfo.ExportTableStructure)
                        Export_TableStructure(tableName);

                    if (ExportInfo.ExportRows)
                        Export_Rows(tableName, selectSql);
                }
            }
        }

        private bool Export_ThisTableIsExcluded(string tableName)
        {
            string tableNameLower = tableName.ToLower();

            foreach (string blacklistedTable in ExportInfo.ExcludeTables)
            {
                if (blacklistedTable.ToLower() == tableNameLower)
                    return true;
            }

            return false;
        }

        private void Export_TableStructure(string tableName)
        {
            if (_stopProcess)
                return;

            Export_WriteComment(string.Empty);
            Export_WriteComment(string.Format("Definition of {0}", tableName));
            Export_WriteComment(string.Empty);

            _textWriter.WriteLine();

            if (ExportInfo.AddDropTable)
                Export_WriteLine(string.Format("DROP TABLE IF EXISTS `{0}`;", tableName));

            if (ExportInfo.ResetAutoIncrement)
                Export_WriteLine(Database.Tables[tableName].CreateTableSqlWithoutAutoIncrement);
            else
                Export_WriteLine(Database.Tables[tableName].CreateTableSql);

            _textWriter.WriteLine();

            _textWriter.Flush();
        }

        private Dictionary<string, string> Export_GetTablesToBeExportedReArranged()
        {
            var dic = Export_GetTablesToBeExported();

            Dictionary<string, string> dic2 = new Dictionary<string, string>();
            foreach (var kv in dic)
            {
                dic2[kv.Key] = Database.Tables[kv.Key].CreateTableSql;
            }

            var lst = Export_ReArrangeDependencies(dic2, "foreign key", "`");
            dic2 = lst.ToDictionary(k => k, k => dic[k]);
            return dic2;
        }

        private Dictionary<string, string> Export_GetTablesToBeExported()
        {
            if (ExportInfo.TablesToBeExportedDic is null ||
                ExportInfo.TablesToBeExportedDic.Count == 0)
            {
                return Database.Tables
                    .ToDictionary(
                        table => table.Name,
                        table => string.Format("SELECT * FROM `{0}`;", table.Name));
            }

            return ExportInfo.TablesToBeExportedDic
                .Where(table => Database.Tables.Contains(table.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private List<string> Export_ReArrangeDependencies(Dictionary<string, string> dic1, string splitKeyword, string keyNameWrapper)
        {
            List<string> lst = new List<string>();
            HashSet<string> index = new HashSet<string>();

            bool requireLoop = true;

            while (requireLoop)
            {
                requireLoop = false;

                foreach (var kv in dic1)
                {
                    if (index.Contains(kv.Key))
                        continue;

                    bool allReferencedAdded = true;

                    string createSql = kv.Value.ToLower();
                    string referenceInfo = string.Empty;

                    bool referenceTaken = false;
                    if (!string.IsNullOrEmpty(splitKeyword))
                    {
                        if (createSql.Contains(string.Format(" {0} ", splitKeyword)))
                        {
                            string[] sa = createSql.Split(new string[] { string.Format(" {0} ", splitKeyword) }, StringSplitOptions.RemoveEmptyEntries);
                            referenceInfo = sa[sa.Length - 1];
                            referenceTaken = true;
                        }
                    }

                    if (!referenceTaken)
                        referenceInfo = createSql;

                    foreach (var kv2 in dic1)
                    {
                        if (kv.Key == kv2.Key)
                            continue;

                        if (index.Contains(kv2.Key))
                            continue;

                        string thisTBname = string.Format("{0}{1}{0}", keyNameWrapper, kv2.Key.ToLower());

                        if (referenceInfo.Contains(thisTBname))
                        {
                            allReferencedAdded = false;
                            break;
                        }
                    }

                    if (allReferencedAdded)
                    {
                        if (!index.Contains(kv.Key))
                        {
                            lst.Add(kv.Key);
                            index.Add(kv.Key);
                            requireLoop = true;
                            break;
                        }
                    }
                }
            }

            foreach (var kv in dic1)
            {
                if (!index.Contains(kv.Key))
                {
                    lst.Add(kv.Key);
                    index.Add(kv.Key);
                }
            }

            return lst;
        }

        private void Export_Rows(string tableName, string selectSql)
        {
            Export_WriteComment(string.Empty);
            Export_WriteComment(string.Format("Dumping data for table {0}", tableName));
            Export_WriteComment(string.Empty);
            _textWriter.WriteLine();
            Export_WriteLine(string.Format("/*!40000 ALTER TABLE `{0}` DISABLE KEYS */;", tableName));

            if (ExportInfo.WrapWithinTransaction)
                Export_WriteLine("START TRANSACTION;");

            Export_RowsData(tableName, selectSql);

            if (ExportInfo.WrapWithinTransaction)
                Export_WriteLine("COMMIT;");

            Export_WriteLine(string.Format("/*!40000 ALTER TABLE `{0}` ENABLE KEYS */;", tableName));
            _textWriter.WriteLine();
            _textWriter.Flush();
        }

        private void Export_RowsData(string tableName, string selectSql)
        {
            _currentRowIndexInCurrentTable = 0L;

            if (ExportInfo.RowsExportMode == RowsDataExportMode.Insert ||
                ExportInfo.RowsExportMode == RowsDataExportMode.InsertIgnore ||
                ExportInfo.RowsExportMode == RowsDataExportMode.Replace)
            {
                Export_RowsData_Insert_Ignore_Replace(tableName, selectSql);
            }
            else if (ExportInfo.RowsExportMode == RowsDataExportMode.OnDuplicateKeyUpdate)
            {
                Export_RowsData_OnDuplicateKeyUpdate(tableName, selectSql);
            }
            else if (ExportInfo.RowsExportMode == RowsDataExportMode.Update)
            {
                Export_RowsData_Update(tableName, selectSql);
            }
        }

        private void Export_RowsData_Insert_Ignore_Replace(string tableName, string selectSql)
        {
            MySqlTable table = Database.Tables[tableName];

            Command.CommandText = selectSql;
            MySqlDataReader rdr = Command.ExecuteReader();

            string insertStatementHeader = null;

            var sb = new StringBuilder((int)ExportInfo.MaxSqlLength);

            while (rdr.Read())
            {
                if (_stopProcess)
                    return;

                _currentRowIndexInAllTable = _currentRowIndexInAllTable + 1;
                _currentRowIndexInCurrentTable = _currentRowIndexInCurrentTable + 1;

                if (insertStatementHeader == null)
                {
                    insertStatementHeader = Export_GetInsertStatementHeader(ExportInfo.RowsExportMode, tableName, rdr);
                }

                string sqlDataRow = Export_GetValueString(rdr, table);

                if (sb.Length == 0)
                {
                    if (ExportInfo.InsertLineBreakBetweenInserts)
                        sb.AppendLine(insertStatementHeader);
                    else
                        sb.Append(insertStatementHeader);
                    sb.Append(sqlDataRow);
                }
                else if ((long)sb.Length + (long)sqlDataRow.Length < ExportInfo.MaxSqlLength)
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

                    Export_WriteLine(sb.ToString());
                    _textWriter.Flush();

                    sb = new StringBuilder((int)ExportInfo.MaxSqlLength);
                    sb.AppendLine(insertStatementHeader);
                    sb.Append(sqlDataRow);
                }
            }

            rdr.Close();

            if (sb.Length > 0)
            {
                sb.Append(";");
            }

            Export_WriteLine(sb.ToString());
            _textWriter.Flush();

            sb = null;
        }

        private void Export_RowsData_OnDuplicateKeyUpdate(string tableName, string selectSql)
        {
            MySqlTable table = Database.Tables[tableName];

            bool allPrimaryField = true;
            foreach (var col in table.Columns)
            {
                if (!col.IsPrimaryKey)
                {
                    allPrimaryField = false;
                    break;
                }
            }

            Command.CommandText = selectSql;
            MySqlDataReader rdr = Command.ExecuteReader();

            while (rdr.Read())
            {
                if (_stopProcess)
                    return;

                _currentRowIndexInAllTable = _currentRowIndexInAllTable + 1;
                _currentRowIndexInCurrentTable = _currentRowIndexInCurrentTable + 1;

                StringBuilder sb = new StringBuilder();

                if (allPrimaryField)
                {
                    sb.Append(Export_GetInsertStatementHeader(RowsDataExportMode.InsertIgnore, tableName, rdr));
                    sb.Append(Export_GetValueString(rdr, table));
                }
                else
                {
                    sb.Append(Export_GetInsertStatementHeader(RowsDataExportMode.Insert, tableName, rdr));
                    sb.Append(Export_GetValueString(rdr, table));
                    sb.Append(" ON DUPLICATE KEY UPDATE ");
                    Export_GetUpdateString(rdr, table, sb);
                }

                sb.Append(";");

                Export_WriteLine(sb.ToString());
                _textWriter.Flush();
            }

            rdr.Close();
        }

        private void Export_RowsData_Update(string tableName, string selectSql)
        {
            MySqlTable table = Database.Tables[tableName];

            bool allPrimaryField = true;
            foreach (var col in table.Columns)
            {
                if (!col.IsPrimaryKey)
                {
                    allPrimaryField = false;
                    break;
                }
            }

            if (allPrimaryField)
                return;

            bool allNonPrimaryField = true;
            foreach (var col in table.Columns)
            {
                if (col.IsPrimaryKey)
                {
                    allNonPrimaryField = false;
                    break;
                }
            }

            if (allNonPrimaryField)
                return;

            Command.CommandText = selectSql;
            MySqlDataReader rdr = Command.ExecuteReader();

            while (rdr.Read())
            {
                if (_stopProcess)
                    return;

                _currentRowIndexInAllTable = _currentRowIndexInAllTable + 1;
                _currentRowIndexInCurrentTable = _currentRowIndexInCurrentTable + 1;

                StringBuilder sb = new StringBuilder();
                sb.Append("UPDATE `");
                sb.Append(tableName);
                sb.Append("` SET ");

                Export_GetUpdateString(rdr, table, sb);

                sb.Append(" WHERE ");

                Export_GetConditionString(rdr, table, sb);

                sb.Append(";");

                Export_WriteLine(sb.ToString());

                _textWriter.Flush();
            }

            rdr.Close();
        }

        private string Export_GetInsertStatementHeader(RowsDataExportMode rowsExportMode, string tableName, MySqlDataReader rdr)
        {
            StringBuilder sb = new StringBuilder();

            if (rowsExportMode == RowsDataExportMode.Insert)
                sb.Append("INSERT INTO `");
            else if (rowsExportMode == RowsDataExportMode.InsertIgnore)
                sb.Append("INSERT IGNORE INTO `");
            else if (rowsExportMode == RowsDataExportMode.Replace)
                sb.Append("REPLACE INTO `");

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

        private string Export_GetValueString(MySqlDataReader rdr, MySqlTable table)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string columnName = rdr.GetName(i);

                if (table.Columns[columnName].IsGeneratedColumn)
                    continue;

                if (sb.Length == 0)
                    sb.AppendFormat("(");
                else
                    sb.AppendFormat(",");


                object ob = rdr[i];
                var col = table.Columns[columnName];

                //sb.Append(QueryExpress.ConvertToSqlFormat(rdr, i, true, true, col));
                sb.Append(QueryExpress.ConvertToSqlFormat(ob, true, true, col, ExportInfo.BlobExportMode));
            }

            sb.AppendFormat(")");
            return sb.ToString();
        }

        private void Export_GetUpdateString(MySqlDataReader rdr, MySqlTable table, StringBuilder sb)
        {
            bool isFirst = true;

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string colName = rdr.GetName(i);

                var col = table.Columns[colName];

                if (!col.IsPrimaryKey)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        sb.Append(",");

                    sb.Append("`");
                    sb.Append(colName);
                    sb.Append("`=");
                    //sb.Append(QueryExpress.ConvertToSqlFormat(rdr, i, true, true, col));
                    sb.Append(QueryExpress.ConvertToSqlFormat(rdr[i], true, true, col, ExportInfo.BlobExportMode));
                }
            }
        }

        private void Export_GetConditionString(MySqlDataReader rdr, MySqlTable table, StringBuilder sb)
        {
            bool isFirst = true;

            for (int i = 0; i < rdr.FieldCount; i++)
            {
                string colName = rdr.GetName(i);

                var col = table.Columns[colName];

                if (col.IsPrimaryKey)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        sb.Append(" and ");

                    sb.Append("`");
                    sb.Append(colName);
                    sb.Append("`=");
                    //sb.Append(QueryExpress.ConvertToSqlFormat(rdr, i, true, true, col));
                    sb.Append(QueryExpress.ConvertToSqlFormat(rdr[i], true, true, col, ExportInfo.BlobExportMode));
                }
            }
        }

        private void Export_Procedures()
        {
            if (!ExportInfo.ExportProcedures || Database.Procedures.Count == 0)
                return;

            Export_WriteComment(string.Empty);
            Export_WriteComment("Dumping procedures");
            Export_WriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlProcedure procedure in Database.Procedures)
            {
                if (_stopProcess)
                    return;

                if (procedure.CreateProcedureSqlWithoutDefiner.Trim().Length == 0 ||
                    procedure.CreateProcedureSql.Trim().Length == 0)
                    continue;

                Export_WriteLine(string.Format("DROP PROCEDURE IF EXISTS `{0}`;", procedure.Name));
                Export_WriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    Export_WriteLine(procedure.CreateProcedureSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    Export_WriteLine(procedure.CreateProcedureSql + " " + ExportInfo.ScriptsDelimiter);

                Export_WriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }
            _textWriter.Flush();
        }

        private void Export_Functions()
        {
            if (!ExportInfo.ExportFunctions || Database.Functions.Count == 0)
                return;

            Export_WriteComment(string.Empty);
            Export_WriteComment("Dumping functions");
            Export_WriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlFunction function in Database.Functions)
            {
                if (_stopProcess)
                    return;

                if (function.CreateFunctionSql.Trim().Length == 0 ||
                    function.CreateFunctionSqlWithoutDefiner.Trim().Length == 0)
                    continue;

                Export_WriteLine(string.Format("DROP FUNCTION IF EXISTS `{0}`;", function.Name));
                Export_WriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    Export_WriteLine(function.CreateFunctionSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    Export_WriteLine(function.CreateFunctionSql + " " + ExportInfo.ScriptsDelimiter);

                Export_WriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }

            _textWriter.Flush();
        }

        private void Export_Views()
        {
            if (!ExportInfo.ExportViews || Database.Views.Count == 0)
                return;

            // ReArrange Views
            Dictionary<string, string> dicViewCreate = new Dictionary<string, string>();
            foreach (var view in Database.Views)
            {
                dicViewCreate[view.Name] = view.CreateViewSql;
            }

            var lst = Export_ReArrangeDependencies(dicViewCreate, null, "`");

            Export_WriteComment(string.Empty);
            Export_WriteComment("Dumping views");
            Export_WriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (var viewname in lst)
            {
                if (_stopProcess)
                    return;

                var view = Database.Views[viewname];

                if (view.CreateViewSql.Trim().Length == 0 ||
                    view.CreateViewSqlWithoutDefiner.Trim().Length == 0)
                    continue;

                Export_WriteLine(string.Format("DROP TABLE IF EXISTS `{0}`;", view.Name));
                Export_WriteLine(string.Format("DROP VIEW IF EXISTS `{0}`;", view.Name));

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    Export_WriteLine(view.CreateViewSqlWithoutDefiner);
                else
                    Export_WriteLine(view.CreateViewSql);

                _textWriter.WriteLine();
            }

            _textWriter.WriteLine();
            _textWriter.Flush();
        }

        private void Export_Events()
        {
            if (!ExportInfo.ExportEvents || Database.Events.Count == 0)
                return;

            Export_WriteComment(string.Empty);
            Export_WriteComment("Dumping events");
            Export_WriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlEvent e in Database.Events)
            {
                if (_stopProcess)
                    return;

                if (e.CreateEventSql.Trim().Length == 0 ||
                    e.CreateEventSqlWithoutDefiner.Trim().Length == 0)
                    continue;

                Export_WriteLine(string.Format("DROP EVENT IF EXISTS `{0}`;", e.Name));
                Export_WriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    Export_WriteLine(e.CreateEventSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    Export_WriteLine(e.CreateEventSql + " " + ExportInfo.ScriptsDelimiter);

                Export_WriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }

            _textWriter.Flush();
        }

        private void Export_Triggers()
        {
            if (!ExportInfo.ExportTriggers ||
                Database.Triggers.Count == 0)
                return;

            Export_WriteComment(string.Empty);
            Export_WriteComment("Dumping triggers");
            Export_WriteComment(string.Empty);
            _textWriter.WriteLine();

            foreach (MySqlTrigger trigger in Database.Triggers)
            {
                if (_stopProcess)
                    return;

                var createTriggerSql = trigger.CreateTriggerSql.Trim();
                var createTriggerSqlWithoutDefiner = trigger.CreateTriggerSqlWithoutDefiner.Trim();
                if (createTriggerSql.Length == 0 ||
                    createTriggerSqlWithoutDefiner.Length == 0)
                    continue;

                Export_WriteLine(string.Format("DROP TRIGGER /*!50030 IF EXISTS */ `{0}`;", trigger.Name));
                Export_WriteLine("DELIMITER " + ExportInfo.ScriptsDelimiter);

                if (ExportInfo.ExportRoutinesWithoutDefiner)
                    Export_WriteLine(createTriggerSqlWithoutDefiner + " " + ExportInfo.ScriptsDelimiter);
                else
                    Export_WriteLine(createTriggerSql + " " + ExportInfo.ScriptsDelimiter);

                Export_WriteLine("DELIMITER ;");
                _textWriter.WriteLine();
            }

            _textWriter.Flush();
        }

        private void Export_DocumentFooter()
        {
            _textWriter.WriteLine();

            List<string> lstFooters = ExportInfo.GetDocumentFooters();
            if (lstFooters.Count > 0)
            {
                foreach (string s in lstFooters)
                {
                    Export_WriteLine(s);
                }
            }

            _timeEnd = DateTime.Now;

            if (ExportInfo.RecordDumpTime)
            {
                TimeSpan ts = _timeEnd - _timeStart;

                _textWriter.WriteLine();
                _textWriter.WriteLine();
                Export_WriteComment(string.Format("Dump completed on {0}", _timeEnd.ToString("yyyy-MM-dd HH:mm:ss")));
                Export_WriteComment(string.Format("Total time: {0}:{1}:{2}:{3}:{4} (d:h:m:s:ms)", ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            }

            _textWriter.Flush();
        }

        private void Export_WriteComment(string text)
        {
            if (ExportInfo.EnableComment)
                Export_WriteLine(string.Format("-- {0}", text));
        }

        private void Export_WriteLine(string text)
        {
            _textWriter.WriteLine(text);
        }

        #endregion

        #region Import

        public void ImportFromString(string sqldumptext)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (StreamWriter thisWriter = new StreamWriter(ms))
                {
                    thisWriter.Write(sqldumptext);
                    thisWriter.Flush();

                    ms.Position = 0L;

                    ImportFromMemoryStream(ms);
                }
            }
        }

        public void ImportFromFile(string filePath)
        {
            System.IO.FileInfo fi = new FileInfo(filePath);

            using (TextReader tr = new StreamReader(filePath))
            {
                ImportFromTextReaderStream(tr, fi);
            }
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
            Import_Start();
        }

        public void ImportFromStream(Stream sm)
        {
            if (sm.CanSeek)
                sm.Seek(0, SeekOrigin.Begin);

            _textReader = new StreamReader(sm);
            Import_Start();
        }

        private void ImportFromTextReaderStream(TextReader tr, FileInfo fileInfo)
        {
            if (fileInfo != null)
                _totalBytes = fileInfo.Length;
            else
                _totalBytes = 0L;

            _textReader = tr;

            Import_Start();
        }

        private void Import_Start()
        {
            Import_InitializeVariables();

            try
            {
                string line = string.Empty;

                while (line != null)
                {
                    if (_stopProcess)
                    {
                        _processCompletionType = ProcessEndType.Cancelled;
                        break;
                    }

                    try
                    {
                        line = Import_GetLine();

                        if (line == null)
                            break;

                        if (line.Length == 0)
                            continue;

                        Import_ProcessLine(line);
                    }
                    catch (Exception ex)
                    {
                        line = string.Empty;
                        LastError = ex;
                        LastErrorSql = _sbImport.ToString();

                        if (!string.IsNullOrEmpty(ImportInfo.ErrorLogFile))
                        {
                            File.AppendAllText(ImportInfo.ErrorLogFile, ex.Message + Environment.NewLine + Environment.NewLine + LastErrorSql + Environment.NewLine + Environment.NewLine);
                        }

                        _sbImport = new StringBuilder();

                        GC.Collect();

                        if (!ImportInfo.IgnoreSqlError)
                        {
                            StopAllProcess();
                            throw;
                        }
                    }
                }
            }
            finally
            {
                ReportEndProcess();
            }
        }

        private void Import_InitializeVariables()
        {
            if (Command == null)
            {
                throw new Exception("MySqlCommand is not initialized. Object not set to an instance of an object.");
            }

            if (Command.Connection == null)
            {
                throw new Exception("MySqlCommand.Connection is not initialized. Object not set to an instance of an object.");
            }

            if (Command.Connection.State != System.Data.ConnectionState.Open)
            {
                throw new Exception("MySqlCommand.Connection is not opened.");
            }

            //_createViewDetected = false;
            //_dicImportRoutines = new Dictionary<string, bool>();
            _stopProcess = false;
            //GetSHA512HashFromPassword(ImportInfo.EncryptionPassword);
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

        private string Import_GetLine()
        {
            string line = _textReader.ReadLine();

            if (line == null)
                return null;

            if (ImportProgressChanged != null)
            {
                _currentBytes = _currentBytes + (long)line.Length;
            }

            line = line.Trim();

            if (Import_IsEmptyLine(line))
            {
                return string.Empty;
            }

            return line;
        }

        private void Import_ProcessLine(string line)
        {
            NextImportAction nextAction = Import_AnalyseNextAction(line);

            switch (nextAction)
            {
                case NextImportAction.Ignore:
                    break;
                case NextImportAction.AppendLine:
                    Import_AppendLine(line);
                    break;
                case NextImportAction.ChangeDelimiter:
                    Import_ChangeDelimiter(line);
                    Import_AppendLine(line);
                    break;
                case NextImportAction.AppendLineAndExecute:
                    Import_AppendLineAndExecute(line);
                    break;
                default:
                    break;
            }
        }

        private NextImportAction Import_AnalyseNextAction(string line)
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

        private void Import_AppendLine(string line)
        {
            _sbImport.AppendLine(line);
        }

        private void Import_ChangeDelimiter(string line)
        {
            string nextDelimiter = line.Substring(9);
            _delimiter = nextDelimiter.Replace(" ", string.Empty);
        }

        private void Import_AppendLineAndExecute(string line)
        {
            _sbImport.Append(line);

            string query = _sbImport.ToString();

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

            _sbImport = new StringBuilder();

            GC.Collect();
        }

        private bool Import_IsEmptyLine(string line)
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

            if (_currentProcess == ProcessType.Export)
            {
                ReportProgress();
                if (ExportCompleted != null)
                {
                    ExportCompleteArgs arg = new ExportCompleteArgs(_timeStart, _timeEnd, _processCompletionType, LastError);
                    ExportCompleted(this, arg);
                }
            }
            else if (_currentProcess == ProcessType.Import)
            {
                _currentBytes = _totalBytes;

                ReportProgress();
                if (ImportCompleted != null)
                {
                    MySqlBackup.ProcessEndType completedType = ProcessEndType.UnknownStatus;
                    switch (_processCompletionType)
                    {
                        case ProcessEndType.Complete:
                            completedType = MySqlBackup.ProcessEndType.Complete;
                            break;
                        case ProcessEndType.Error:
                            completedType = MySqlBackup.ProcessEndType.Error;
                            break;
                        case ProcessEndType.Cancelled:
                            completedType = MySqlBackup.ProcessEndType.Cancelled;
                            break;
                    }

                    ImportCompleteArgs arg = new ImportCompleteArgs(completedType, _timeStart, _timeEnd, LastError);
                    ImportCompleted(this, arg);
                }
            }
        }

        private void timerReport_Elapsed(object sender, ElapsedEventArgs e)
        {
            ReportProgress();
        }

        private void ReportProgress()
        {
            if (_currentProcess == ProcessType.Export)
            {
                if (ExportProgressChanged != null)
                {
                    ExportProgressArgs arg = new ExportProgressArgs(_currentTableName, _totalRowsInCurrentTable, _totalRowsInAllTables, _currentRowIndexInCurrentTable, _currentRowIndexInAllTable, _totalTables, _currentTableIndex);
                    ExportProgressChanged(this, arg);
                }
            }
            else if (_currentProcess == ProcessType.Import)
            {
                if (ImportProgressChanged != null)
                {
                    ImportProgressArgs arg = new ImportProgressArgs(_currentBytes, _totalBytes);
                    ImportProgressChanged(this, arg);
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
