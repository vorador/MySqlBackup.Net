using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace Devart.Data.MySql
{
    public class MySqlViewList : IDisposable, IEnumerable<MySqlView>
    {
        private Dictionary<string, MySqlView> _lst = new Dictionary<string, MySqlView>();

        public bool AllowAccess { get; } = true;

        public string SqlShowViewList { get; } = string.Empty;

        public MySqlViewList()
        { }

        public MySqlViewList(MySqlCommand cmd)
        {
            try
            {
                string dbname = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
                SqlShowViewList = string.Format("SHOW FULL TABLES FROM `{0}` WHERE Table_type = 'VIEW';", dbname);
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
            catch
            {
                throw;
            }
        }

        public MySqlView this[string viewName]
        {
            get
            {
                if (_lst.ContainsKey(viewName))
                    return _lst[viewName];

                throw new Exception("View \"" + viewName + "\" is not existed.");
            }
        }

        public int Count
        {
            get
            {
                return _lst.Count;
            }
        }

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
            foreach (var key in _lst.Keys)
            {
                _lst[key] = null;
            }
            _lst = null;
        }
    }
}
