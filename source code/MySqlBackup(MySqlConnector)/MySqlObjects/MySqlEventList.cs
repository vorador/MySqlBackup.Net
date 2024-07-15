using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace MySqlConnector
{
    public class MySqlEventList : IDisposable, IEnumerable<MySqlEvent>
    {
        private Dictionary<string, MySqlEvent> _lst = new();

        public bool AllowAccess { get; } = true;

        public string SqlShowEvent { get; } = string.Empty;

        public MySqlEventList()
        { }

        public MySqlEventList(MySqlCommand cmd)
        {
            try
            {
                string dbname = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
                SqlShowEvent = $"SHOW EVENTS WHERE UPPER(TRIM(Db))=UPPER(TRIM('{dbname}'));";
                DataTable dt = QueryExpress.GetTable(cmd, SqlShowEvent);

                foreach (DataRow dr in dt.Rows)
                {
                    var eventName = dr["Name"].ToString();
                    _lst.Add(eventName, new MySqlEvent(cmd, eventName, dr["Definer"].ToString()));
                }
            }
            catch (MySqlException myEx)
            {
                if (myEx.Message.ToLower().Contains("access denied"))
                    AllowAccess = false;
            }
        }

        public MySqlEvent this[string eventName]
        {
            get
            {
                if (_lst.TryGetValue(eventName, out MySqlEvent mySqlEvent))
                    return mySqlEvent;

                throw new Exception("Event \"" + eventName + "\" is not existed.");
            }
        }

        public int Count => _lst.Count;

        public bool Contains(string eventName)
        {
            return _lst.ContainsKey(eventName);
        }

        public IEnumerator<MySqlEvent> GetEnumerator() =>
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
