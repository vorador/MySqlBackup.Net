using System;
using System.Data;
using System.Linq;
using System.Timers;

namespace MySqlConnector
{
    public class MySqlDatabase : IDisposable
    {
        public string Name { get; private set; } = string.Empty;
        public string DefaultCharacterSet { get; private set; } = string.Empty;
        public string CreateDatabaseSql { get; private set; } = string.Empty;
        public string DropDatabaseSql { get; private set; } = string.Empty;

        public MySqlTableList Tables { get; private set; } = new();
        public MySqlProcedureList Procedures { get; private set; } = new();
        public MySqlEventList Events { get; private set; } = new();
        public MySqlViewList Views { get; private set; } = new();
        public MySqlFunctionList Functions { get; private set; } = new();
        public MySqlTriggerList Triggers { get; private set; } = new();

        public delegate void GetTotalRowsProgressChange(object sender, GetTotalRowsArgs e);
        public event GetTotalRowsProgressChange GetTotalRowsProgressChanged;

        public long TotalRows
        {
            get
            {
                return Tables.ToList().Sum(x => x.TotalRows);
            }
        }

        public MySqlDatabase()
        { }

        public void GetDatabaseInfo(MySqlCommand cmd, GetTotalRowsMethod enumGetTotalRowsMode)
        {
            Name = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
            DefaultCharacterSet = QueryExpress.ExecuteScalarStr(cmd, "SHOW VARIABLES LIKE 'character_set_database';", 1);
            CreateDatabaseSql = QueryExpress.ExecuteScalarStr(cmd, $"SHOW CREATE DATABASE `{Name}`;", 1).Replace("CREATE DATABASE", "CREATE DATABASE IF NOT EXISTS") + ";";
            DropDatabaseSql = $"DROP DATABASE IF EXISTS `{Name}`;";

            Tables = new MySqlTableList(cmd);
            Procedures = new MySqlProcedureList(cmd);
            Functions = new MySqlFunctionList(cmd);
            Triggers = new MySqlTriggerList(cmd);
            Events = new MySqlEventList(cmd);
            Views = new MySqlViewList(cmd);

            if (enumGetTotalRowsMode != GetTotalRowsMethod.Skip)
                GetTotalRows(cmd, enumGetTotalRowsMode);
        }

        public void GetTotalRows(MySqlCommand cmd, GetTotalRowsMethod enumGetTotalRowsMode)
        {
            int i = 0;
            var timer = new Timer
            {
                Interval = 10000
            };
            timer.Elapsed += (sender, e) =>
            {
                GetTotalRowsProgressChanged?.Invoke(this, new GetTotalRowsArgs(Tables.Count, i));
            };

            switch (enumGetTotalRowsMode)
            {
                case GetTotalRowsMethod.InformationSchema:
                {
                    DataTable dtTotalRows = QueryExpress.GetTable(cmd,
                        $"SELECT TABLE_NAME, TABLE_ROWS FROM `information_schema`.`tables` WHERE `table_schema` = '{Name}';");
                    timer.Start();
                    foreach (DataRow dr in dtTotalRows.Rows)
                    {
                        i++;
                        string tbname = dr["TABLE_NAME"] + "";
                        long.TryParse(dr["TABLE_ROWS"] + "", out long totalRowsThisTable);

                        if (Tables.Contains(tbname))
                            Tables[tbname].SetTotalRows((long)(totalRowsThisTable * 1.1)); // Adiciona 10% de erro
                    }
                    timer.Stop();
                    GetTotalRowsProgressChanged?.Invoke(this, new GetTotalRowsArgs(Tables.Count, Tables.Count));
                    break;
                }
                case GetTotalRowsMethod.SelectCount:
                {
                    timer.Start();
                    foreach (MySqlTable table in Tables)
                    {
                        i++;
                        table.GetTotalRowsByCounting(cmd);
                    }
                    timer.Stop();
                    GetTotalRowsProgressChanged?.Invoke(this, new GetTotalRowsArgs(Tables.Count, Tables.Count));
                    break;
                }
            }
        }

        public void Dispose()
        {
            Tables.Dispose();
            Procedures.Dispose();
            Functions.Dispose();
            Events.Dispose();
            Triggers.Dispose();
            Views.Dispose();
        }
    }
}
