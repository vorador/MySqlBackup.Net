using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace MySql.Data.MySqlClient
{
    public class MySqlProcedureList : IDisposable, IEnumerable<MySqlProcedure>
    {
        private Dictionary<string, MySqlProcedure> _lst = new();

        public bool AllowAccess { get; } = true;

        public string SqlShowProcedures { get; } = string.Empty;

        public MySqlProcedureList()
        { }

        public MySqlProcedureList(MySqlCommand cmd)
        {
            try
            {
                string dbname = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
                SqlShowProcedures = string.Format("SHOW PROCEDURE STATUS WHERE UPPER(TRIM(Db))= UPPER(TRIM('{0}'));", dbname);
                DataTable dt = QueryExpress.GetTable(cmd, SqlShowProcedures);

                foreach (DataRow dr in dt.Rows)
                {
                    var name = dr["Name"].ToString();
                    _lst.Add(name, new MySqlProcedure(cmd, name, dr["Definer"].ToString()));
                }
            }
            catch (MySqlException myEx)
            {
                if (myEx.Message.ToLower().Contains("access denied"))
                    AllowAccess = false;
            }
            catch
            {
                throw;
            }
        }

        public MySqlProcedure this[string procedureName]
        {
            get
            {
                if (_lst.ContainsKey(procedureName))
                    return _lst[procedureName];

                throw new Exception("Store procedure \"" + procedureName + "\" is not existed.");
            }
        }

        public int Count
        {
            get
            {
                return _lst.Count;
            }
        }

        public bool Contains(string procedureName)
        {
            return _lst.ContainsKey(procedureName);
        }

        public IEnumerator<MySqlProcedure> GetEnumerator() =>
            _lst.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
           _lst.Values.GetEnumerator();

        public void Dispose()
        {
            foreach (var key in _lst.Keys)
            {
                _lst[key] = null;
            }
            _lst = null;
        }
    }
}
