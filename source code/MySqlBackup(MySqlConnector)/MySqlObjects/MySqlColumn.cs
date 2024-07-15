using System;

namespace MySqlConnector
{
    public class MySqlColumn
    {
        public enum DataWrapper
        {
            None,
            Sql
        }

        private readonly int _timeFractionLength = 0;

        public string Name { get; } = string.Empty;
        public Type DataType { get; } = typeof(string);
        public string MySqlDataType { get; } = string.Empty;
        public string Collation { get; } = string.Empty;
        public bool AllowNull { get; } = true;
        public string Key { get; } = string.Empty;
        public string DefaultValue { get; } = string.Empty;
        public string Extra { get; } = string.Empty;
        public string Privileges { get; } = string.Empty;
        public string Comment { get; } = string.Empty;
        public bool IsPrimaryKey { get; }
        public int TimeFractionLength => _timeFractionLength;
        public bool IsGeneratedColumn { get; }

        public MySqlColumn(string name, Type type, string mySqlDataType,
            string collation, bool allowNull, string key, string defaultValue,
            string extra, string privileges, string comment)
        {
            Name = name;
            DataType = type;
            MySqlDataType = mySqlDataType.ToLower();
            Collation = collation;
            AllowNull = allowNull;
            Key = key;
            DefaultValue = defaultValue;
            Extra = extra;
            Privileges = privileges;
            Comment = comment;

            if (key.ToLower() == "pri")
                IsPrimaryKey = true;

            if (DataType == typeof(DateTime))
            {
                if (MySqlDataType.Length > 8)
                {
                    string fractionLength = string.Empty;
                    foreach (var dL in MySqlDataType)
                    {
                        if (char.IsNumber(dL))
                            fractionLength += Convert.ToString(dL);
                    }

                    if (fractionLength.Length > 0)
                    {
                        _timeFractionLength = 0;
                        int.TryParse(fractionLength, out _timeFractionLength);
                    }
                }
            }

            if (Extra.ToUpper() == "VIRTUAL GENERATED" || Extra.ToUpper()== "STORED GENERATED")
                IsGeneratedColumn = true;
            else
                IsGeneratedColumn = false;
        }
    }
}