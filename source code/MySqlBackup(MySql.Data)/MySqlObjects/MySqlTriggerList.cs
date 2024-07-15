using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace MySql.Data.MySqlClient
{
    public class MySqlTriggerList : IDisposable, IEnumerable<MySqlTrigger>
    {
        private Dictionary<string, MySqlTrigger> _lst = new();

        public bool AllowAccess { get; } = true;

        public string SqlShowTriggers { get; } = string.Empty;

        public MySqlTriggerList()
        { }

        public MySqlTriggerList(MySqlCommand cmd)
        {
            SqlShowTriggers = "SHOW TRIGGERS;";
            try
            {
                DataTable dt = QueryExpress.GetTable(cmd, SqlShowTriggers);

                foreach (DataRow dr in dt.Rows)
                {
                    var name = dr["Trigger"].ToString();
                    _lst.Add(name, new MySqlTrigger(cmd, name, dr["Definer"].ToString()));
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

        public MySqlTrigger this[string triggerName]
        {
            get
            {
                if (_lst.ContainsKey(triggerName))
                    return _lst[triggerName];

                throw new Exception("Trigger \"" + triggerName + "\" is not existed.");
            }
        }

        public int Count
        {
            get
            {
                return _lst.Count;
            }
        }

        public bool Contains(string triggerName)
        {
            return _lst.ContainsKey(triggerName);
        }

        public IEnumerator<MySqlTrigger> GetEnumerator() =>
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
