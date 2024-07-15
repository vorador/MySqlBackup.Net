using System;
using System.Collections.Generic;
using System.Text;

namespace MySql.Data.MySqlClient
{
    public class MySqlProcedure
    {
        public string Name { get; }
        public string CreateProcedureSql { get; }
        public string CreateProcedureSqlWithoutDefiner { get; }

        public MySqlProcedure(MySqlCommand cmd, string procedureName, string definer)
        {
            Name = procedureName;

            string sql = string.Format("SHOW CREATE PROCEDURE `{0}`;", procedureName);

            CreateProcedureSql = QueryExpress.ExecuteScalarStr(cmd, sql, 2);

            CreateProcedureSql = CreateProcedureSql.Replace("\r\n", "^~~~~~~~~~~~~~~^");
            CreateProcedureSql = CreateProcedureSql.Replace("\n", "^~~~~~~~~~~~~~~^");
            CreateProcedureSql = CreateProcedureSql.Replace("\r", "^~~~~~~~~~~~~~~^");
            CreateProcedureSql = CreateProcedureSql.Replace("^~~~~~~~~~~~~~~^", "\r\n");

            string[] sa = definer.Split('@');
            definer = string.Format(" DEFINER=`{0}`@`{1}`", sa[0], sa[1]);

            CreateProcedureSqlWithoutDefiner = CreateProcedureSql.Replace(definer, string.Empty);
        }
    }
}
