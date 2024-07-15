namespace MySql.Data.MySqlClient
{
    public class MySqlEvent
    {
        public string Name { get; }
        public string CreateEventSql { get; } = string.Empty;
        public string CreateEventSqlWithoutDefiner { get; } = string.Empty;

        public MySqlEvent(MySqlCommand cmd, string eventName, string definer)
        {
            Name = eventName;

            CreateEventSql = QueryExpress.ExecuteScalarStr(cmd, string.Format("SHOW CREATE EVENT `{0}`;", Name), "Create Event");

            CreateEventSql = CreateEventSql.Replace("\r\n", "^~~~~~~~~~~~~~~^");
            CreateEventSql = CreateEventSql.Replace("\n", "^~~~~~~~~~~~~~~^");
            CreateEventSql = CreateEventSql.Replace("\r", "^~~~~~~~~~~~~~~^");
            CreateEventSql = CreateEventSql.Replace("^~~~~~~~~~~~~~~^", "\r\n");

            string[] sa = definer.Split('@');
            definer = string.Format(" DEFINER=`{0}`@`{1}`", sa[0], sa[1]);

            CreateEventSqlWithoutDefiner = CreateEventSql.Replace(definer, string.Empty);
        }
    }
}
