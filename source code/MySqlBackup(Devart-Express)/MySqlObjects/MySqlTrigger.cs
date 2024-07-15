namespace Devart.Data.MySql
{
    public class MySqlTrigger
    {
        public string Name { get; } = string.Empty;
        public string CreateTriggerSql { get; } = string.Empty;
        public string CreateTriggerSqlWithoutDefiner { get; } = string.Empty;

        public MySqlTrigger(MySqlCommand cmd, string triggerName, string definer)
        {
            Name = triggerName;

            CreateTriggerSql = QueryExpress.ExecuteScalarStr(cmd, string.Format("SHOW CREATE TRIGGER `{0}`;", triggerName), 2);

            CreateTriggerSql = CreateTriggerSql.Replace("\r\n", "^~~~~~~~~~~~~~~^");
            CreateTriggerSql = CreateTriggerSql.Replace("\n", "^~~~~~~~~~~~~~~^");
            CreateTriggerSql = CreateTriggerSql.Replace("\r", "^~~~~~~~~~~~~~~^");
            CreateTriggerSql = CreateTriggerSql.Replace("^~~~~~~~~~~~~~~^", "\r\n");

            string[] sa = definer.Split('@');
            definer = string.Format(" DEFINER=`{0}`@`{1}`", sa[0], sa[1]);

            CreateTriggerSqlWithoutDefiner = CreateTriggerSql.Replace(definer, string.Empty);
        }
    }
}
