namespace MySql.Data.MySqlClient
{
    public class MySqlFunction
    {
        public string Name { get; }
        public string CreateFunctionSql { get; } = string.Empty;
        public string CreateFunctionSqlWithoutDefiner { get; } = string.Empty;

        public MySqlFunction(MySqlCommand cmd, string functionName, string definer)
        {
            Name = functionName;

            string sql = string.Format("SHOW CREATE FUNCTION `{0}`;", functionName);

            CreateFunctionSql = QueryExpress.ExecuteScalarStr(cmd, sql, 2);

            CreateFunctionSql = CreateFunctionSql.Replace("\r\n", "^~~~~~~~~~~~~~~^");
            CreateFunctionSql = CreateFunctionSql.Replace("\n", "^~~~~~~~~~~~~~~^");
            CreateFunctionSql = CreateFunctionSql.Replace("\r", "^~~~~~~~~~~~~~~^");
            CreateFunctionSql = CreateFunctionSql.Replace("^~~~~~~~~~~~~~~^", "\r\n");

            string[] sa = definer.Split('@');
            definer = string.Format(" DEFINER=`{0}`@`{1}`", sa[0], sa[1]);

            CreateFunctionSqlWithoutDefiner = CreateFunctionSql.Replace(definer, string.Empty);
        }
    }
}
