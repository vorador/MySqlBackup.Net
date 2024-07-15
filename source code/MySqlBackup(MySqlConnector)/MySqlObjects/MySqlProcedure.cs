namespace MySqlConnector
{
    public class MySqlProcedure
    {
        public string Name { get; }
        public string CreateProcedureSql { get; }
        public string CreateProcedureSqlWithoutDefiner { get; }

        public MySqlProcedure(MySqlCommand cmd, string procedureName, string definer)
        {
            Name = procedureName;

            string sql = $"SHOW CREATE PROCEDURE `{procedureName}`;";

            CreateProcedureSql = QueryExpress.ExecuteScalarStr(cmd, sql, 2);

            CreateProcedureSql = CreateProcedureSql.Replace("\r\n", "^~~~~~~~~~~~~~~^");
            CreateProcedureSql = CreateProcedureSql.Replace("\n", "^~~~~~~~~~~~~~~^");
            CreateProcedureSql = CreateProcedureSql.Replace("\r", "^~~~~~~~~~~~~~~^");
            CreateProcedureSql = CreateProcedureSql.Replace("^~~~~~~~~~~~~~~^", "\r\n");

            string[] sa = definer.Split('@');
            definer = $" DEFINER=`{sa[0]}`@`{sa[1]}`";

            CreateProcedureSqlWithoutDefiner = CreateProcedureSql.Replace(definer, string.Empty);
        }
    }
}
