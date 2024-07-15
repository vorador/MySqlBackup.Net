using System;
using System.Data;
using System.Linq;
using System.Timers;

namespace MySql.Data.MySqlClient
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
            CreateDatabaseSql = QueryExpress.ExecuteScalarStr(cmd, string.Format("SHOW CREATE DATABASE `{0}`;", Name), 1).Replace("CREATE DATABASE", "CREATE DATABASE IF NOT EXISTS") + ";";
            DropDatabaseSql = string.Format("DROP DATABASE IF EXISTS `{0}`;", Name);

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

            if (enumGetTotalRowsMode == GetTotalRowsMethod.InformationSchema)
            {
                DataTable dtTotalRows = QueryExpress.GetTable(cmd, string.Format("SELECT TABLE_NAME, TABLE_ROWS FROM `information_schema`.`tables` WHERE `table_schema` = '{0}';", Name));
                timer.Start();
                foreach (DataRow dr in dtTotalRows.Rows)
                {
                    i++;
                    var tbname = dr["TABLE_NAME"] + "";
                    long.TryParse(dr["TABLE_ROWS"] + "", out var totalRowsThisTable);

                    if (Tables.Contains(tbname))
                        Tables[tbname].SetTotalRows((long)(totalRowsThisTable * 1.1)); // Adiciona 10% de erro
                }
                timer.Stop();
                GetTotalRowsProgressChanged?.Invoke(this, new GetTotalRowsArgs(Tables.Count, Tables.Count));
            }
            else if (enumGetTotalRowsMode == GetTotalRowsMethod.SelectCount)
            {
                timer.Start();
                foreach (var table in Tables)
                {
                    i++;
                    table.GetTotalRowsByCounting(cmd);
                }
                timer.Stop();
                GetTotalRowsProgressChanged?.Invoke(this, new GetTotalRowsArgs(Tables.Count, Tables.Count));
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
