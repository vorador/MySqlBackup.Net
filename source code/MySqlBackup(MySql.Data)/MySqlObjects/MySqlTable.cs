using System;
using System.Text;

namespace MySql.Data.MySqlClient
{
    public class MySqlTable : IDisposable
    {
        public string Name { get; } = string.Empty;
        public long TotalRows { get; private set; } = 0;
        public string CreateTableSql { get; } = string.Empty;
        public string CreateTableSqlWithoutAutoIncrement { get; } = string.Empty;
        public MySqlColumnList Columns { get; private set; } = null;
        public string InsertStatementHeaderWithoutColumns { get; private set; } = string.Empty;
        public string InsertStatementHeader { get; private set; } = string.Empty;

        public MySqlTable(MySqlCommand cmd, string name)
        {
            Name = name;
            string sql = string.Format("SHOW CREATE TABLE `{0}`;", name);
            CreateTableSql = QueryExpress.ExecuteScalarStr(cmd, sql, 1).Replace(Environment.NewLine, "^~~~~~~^").Replace("\r", "^~~~~~~^").Replace("\n", "^~~~~~~^").Replace("^~~~~~~^", Environment.NewLine).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ") + ";";
            CreateTableSqlWithoutAutoIncrement = RemoveAutoIncrement(CreateTableSql);
            Columns = new MySqlColumnList(cmd, name);
            GetInsertStatementHeaders();
        }

        private void GetInsertStatementHeaders()
        {
            InsertStatementHeaderWithoutColumns = string.Format("INSERT INTO `{0}` VALUES", Name);

            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO `");
            sb.Append(Name);
            sb.Append("` (");
            var i = 0;
            foreach (var column in Columns)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append('`');
                sb.Append(column.Name);
                sb.Append('`');
                i++;
            }
            sb.Append(") VALUES");

            InsertStatementHeader = sb.ToString();
        }

        public void GetTotalRowsByCounting(MySqlCommand cmd)
        {
            string sql = string.Format("SELECT COUNT(1) FROM `{0}`;", Name);
            TotalRows = QueryExpress.ExecuteScalarLong(cmd, sql);
        }

        public void SetTotalRows(long trows)
        {
            TotalRows = trows;
        }

        private string RemoveAutoIncrement(string sql)
        {
            string a = "AUTO_INCREMENT=";

            if (sql.Contains(a))
            {
                int i = sql.LastIndexOf(a);

                int b = i + a.Length;

                string d = string.Empty;

                int count = 0;

                while (char.IsDigit(sql[b + count]))
                {
                    char cc = sql[b + count];

                    d = d + cc;

                    count = count + 1;
                }

                sql = sql.Replace(a + d, string.Empty);
            }

            return sql;
        }

        public void Dispose()
        {
            Columns.Dispose();
            Columns = null;
        }
    }
}
