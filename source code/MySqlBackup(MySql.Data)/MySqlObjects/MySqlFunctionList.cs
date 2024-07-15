using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace MySql.Data.MySqlClient
{
    public class MySqlFunctionList : IDisposable, IEnumerable<MySqlFunction>
    {
        private Dictionary<string, MySqlFunction> _lst = new();

        public bool AllowAccess { get; } = true;

        public string SqlShowFunctions { get; } = string.Empty;

        public MySqlFunctionList()
        { }

        public MySqlFunctionList(MySqlCommand cmd)
        {
            try
            {
                string dbname = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
                SqlShowFunctions = string.Format("SHOW FUNCTION STATUS WHERE UPPER(TRIM(Db))= UPPER(TRIM('{0}'));", dbname);
                DataTable dt = QueryExpress.GetTable(cmd, SqlShowFunctions);

                foreach (DataRow dr in dt.Rows)
                {
                    var name = dr["Name"].ToString();
                    _lst.Add(name, new MySqlFunction(cmd, name, dr["Definer"].ToString()));
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

        public MySqlFunction this[string functionName]
        {
            get
            {
                if (_lst.ContainsKey(functionName))
                    return _lst[functionName];

                throw new Exception("Function \"" + functionName + "\" is not existed.");
            }
        }

        public int Count
        {
            get
            {
                return _lst.Count;
            }
        }

        public bool Contains(string functionName)
        {
            return _lst.ContainsKey(functionName);
        }

        public IEnumerator<MySqlFunction> GetEnumerator() =>
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
