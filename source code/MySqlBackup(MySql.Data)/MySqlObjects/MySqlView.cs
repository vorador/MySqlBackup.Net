namespace MySql.Data.MySqlClient
{
    public class MySqlView
    {
        public string Name { get; } = string.Empty;
        public string CreateViewSql { get; } = string.Empty;
        public string CreateViewSqlWithoutDefiner { get; } = string.Empty;

        public MySqlView(MySqlCommand cmd, string viewName)
        {
            Name = viewName;

            string sqlShowCreate = string.Format("SHOW CREATE VIEW `{0}`;", viewName);

            System.Data.DataTable dtView = QueryExpress.GetTable(cmd, sqlShowCreate);

            CreateViewSql = dtView.Rows[0]["Create View"] + ";";

            CreateViewSql = CreateViewSql.Replace("\r\n", "^~~~~~~~~~~~~~~^");
            CreateViewSql = CreateViewSql.Replace("\n", "^~~~~~~~~~~~~~~^");
            CreateViewSql = CreateViewSql.Replace("\r", "^~~~~~~~~~~~~~~^");
            CreateViewSql = CreateViewSql.Replace("^~~~~~~~~~~~~~~^", "\r\n");

            CreateViewSqlWithoutDefiner = QueryExpress.EraseDefiner(CreateViewSql);
        }
    }
}
