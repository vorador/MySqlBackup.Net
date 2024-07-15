﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace Devart.Data.MySql
{
    public class MySqlEventList : IDisposable, IEnumerable<MySqlEvent>
    {
        private Dictionary<string, MySqlEvent> _lst = new Dictionary<string, MySqlEvent>();

        public bool AllowAccess { get; } = true;

        public string SqlShowEvent { get; } = string.Empty;

        public MySqlEventList()
        { }

        public MySqlEventList(MySqlCommand cmd)
        {
            try
            {
                string dbname = QueryExpress.ExecuteScalarStr(cmd, "SELECT DATABASE();");
                SqlShowEvent = string.Format("SHOW EVENTS WHERE UPPER(TRIM(Db))=UPPER(TRIM('{0}'));", dbname);
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
            catch
            {
                throw;
            }
        }

        public MySqlEvent this[string eventName]
        {
            get
            {
                if (_lst.ContainsKey(eventName))
                    return _lst[eventName];

                throw new Exception("Event \"" + eventName + "\" is not existed.");
            }
        }

        public int Count
        {
            get
            {
                return _lst.Count;
            }
        }

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
            foreach (var key in _lst.Keys)
            {
                _lst[key] = null;
            }
            _lst = null;
        }
    }
}
