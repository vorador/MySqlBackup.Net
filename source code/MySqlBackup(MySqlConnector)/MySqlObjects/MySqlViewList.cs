using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace MySqlConnector
{
    public class MySqlViewList : IDisposable, IEnumerable<MySqlView>
    {
        private Dictionary<string, MySqlView> _lst = new();

        public bool AllowAccess { get; } = true;

        public string SqlShowViewList { get; } = string.Empty;

        public MySqlViewList()
        { }

        public MySqlViewList(MySqlCommand cmd)
        {
            try
            {
                string dbname = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
                SqlShowViewList = $"SHOW FULL TABLES FROM `{dbname}` WHERE Table_type = 'VIEW';";
                DataTable dt = QueryExpress.GetTable(cmd, SqlShowViewList);

                foreach (DataRow dr in dt.Rows)
                {
                    var nome = dr[0].ToString();
                    _lst.Add(nome, new MySqlView(cmd, nome));
                }
            }
            catch (MySqlException myEx)
            {
                if (myEx.Message.ToLower().Contains("access denied"))
                    AllowAccess = false;
            }
        }

        public MySqlView this[string viewName]
        {
            get
            {
                if (_lst.TryGetValue(viewName, out MySqlView view))
                    return view;

                throw new Exception("View \"" + viewName + "\" is not existed.");
            }
        }

        public int Count => _lst.Count;

        public bool Contains(string viewName)
        {
            return _lst.ContainsKey(viewName);
        }

        public IEnumerator<MySqlView> GetEnumerator() =>
            _lst.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
           _lst.Values.GetEnumerator();

        public void Dispose()
        {
            foreach (string key in _lst.Keys)
                _lst[key] = null;
            
            _lst = null;
        }
    }
}