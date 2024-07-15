using System;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MySqlConnector
{
    public class QueryExpress
    {
        public static NumberFormatInfo MySqlNumberFormat { get; } = new()
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = string.Empty
        };

        public static DateTimeFormatInfo MySqlDateTimeFormat { get; } = new()
        {
            DateSeparator = "-",
            TimeSeparator = ":"
        };

        public static DataTable GetTable(MySqlCommand cmd, string sql)
        {
            DataTable dt = new DataTable();
            cmd.CommandText = sql;
            
            using MySqlDataAdapter da = new MySqlDataAdapter(cmd);
            da.Fill(dt);
            
            return dt;
        }

        public static string ExecuteScalarStr(MySqlCommand cmd, string sql)
        {
            cmd.CommandText = sql;
            object ob = cmd.ExecuteScalar();
            
            if (ob is byte[] bytes)
                return Encoding.UTF8.GetString(bytes);
            
            return ob + "";
        }

        public static string ExecuteScalarStr(MySqlCommand cmd, string sql, int columnIndex)
        {
            DataTable dt = GetTable(cmd, sql);

            if (dt.Rows[0][columnIndex] is byte[])
                return Encoding.UTF8.GetString((byte[])dt.Rows[0][columnIndex]);
            
            return dt.Rows[0][columnIndex] + "";
        }

        public static string ExecuteScalarStr(MySqlCommand cmd, string sql, string columnName)
        {
            DataTable dt = GetTable(cmd, sql);

            if (dt.Rows[0][columnName] is byte[])
                return Encoding.UTF8.GetString((byte[])dt.Rows[0][columnName]);
            
            return dt.Rows[0][columnName] + "";
        }

        public static long ExecuteScalarLong(MySqlCommand cmd, string sql)
        {
            long l = 0;
            cmd.CommandText = sql;
            long.TryParse(cmd.ExecuteScalar() + "", out l);
            return l;
        }

        public static string EscapeStringSequence(string data)
        {
            var builder = new StringBuilder();

            foreach (char c in data)
                EscapeString(builder, c);

            return builder.ToString();
        }

        private static void EscapeString(StringBuilder sb, char c)
        {
            switch (c)
            {
                case '\\': // Backslash
                    sb.Append("\\\\");
                    break;
                case '\0': // Null
                    sb.Append("\\0");
                    break;
                case '\r': // Carriage return
                    sb.Append("\\r");
                    break;
                case '\n': // New Line
                    sb.Append("\\n");
                    break;
                case '\a': // Vertical tab
                    sb.Append("\\a");
                    break;
                case '\b': // Backspace
                    sb.Append("\\b");
                    break;
                case '\f': // Formfeed
                    sb.Append("\\f");
                    break;
                case '\t': // Horizontal tab
                    sb.Append("\\t");
                    break;
                case '\v': // Vertical tab
                    sb.Append("\\v");
                    break;
                case '\"': // Double quotation mark
                    sb.Append("\\\"");
                    break;
                case '\'': // Single quotation mark
                    sb.Append("''");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        public static string EraseDefiner(string input)
        {
            StringBuilder sb = new StringBuilder();
            string definer = " DEFINER=";
            int dIndex = input.IndexOf(definer);

            sb.AppendFormat(definer);

            bool pointAliasReached = false;
            bool point3RdQuoteReached = false;

            for (int i = dIndex + definer.Length; i < input.Length; i++)
            {
                if (!pointAliasReached)
                {
                    if (input[i] == '@')
                        pointAliasReached = true;

                    sb.Append(input[i]);
                    continue;
                }

                if (!point3RdQuoteReached)
                {
                    if (input[i] == '`')
                        point3RdQuoteReached = true;

                    sb.Append(input[i]);
                    continue;
                }

                if (input[i] != '`')
                {
                    sb.Append(input[i]);
                    continue;
                }
                else
                {
                    sb.Append(input[i]);
                    break;
                }
            }

            return input.Replace(sb.ToString(), string.Empty);
        }

        public static string ConvertToSqlFormat(object ob, bool wrapStringWithSingleQuote, bool escapeStringSequence, MySqlColumn col, BlobDataExportMode blobExportMode)
        {
            StringBuilder sb = new StringBuilder();

            switch (ob)
            {
                case null or System.DBNull:
                    sb.AppendFormat("NULL");
                    break;
                case string strObj:
                {
                    string str = strObj;

                    if (escapeStringSequence)
                        str = QueryExpress.EscapeStringSequence(str);

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    sb.Append(str);

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    break;
                }
                case bool:
                    sb.AppendFormat(Convert.ToInt32(ob).ToString());
                    break;
                case byte[] { Length: 0 }:
                {
                    return wrapStringWithSingleQuote ? "''" : "";
                }
                case byte[] byteObj when blobExportMode == BlobDataExportMode.HexString:
                    sb.AppendFormat(CryptoExpress.ConvertByteArrayToHexString(byteObj));
                    break;
                case byte[] byteObj:
                {
                    if (blobExportMode == BlobDataExportMode.BinaryChar)
                    {
                        if (wrapStringWithSingleQuote)
                            sb.Append("'");

                        foreach (byte b in byteObj)
                        {
                            char ch = (char)b;
                            EscapeString(sb, ch);
                        }

                        if (wrapStringWithSingleQuote)
                            sb.Append("'");
                    }

                    break;
                }
                case short strObj:
                    sb.AppendFormat(strObj.ToString(MySqlNumberFormat));
                    break;
                case int intObj:
                    sb.AppendFormat(intObj.ToString(MySqlNumberFormat));
                    break;
                case long longObj:
                    sb.AppendFormat(longObj.ToString(MySqlNumberFormat));
                    break;
                case ushort shortObj:
                    sb.AppendFormat(shortObj.ToString(MySqlNumberFormat));
                    break;
                case uint uIntObj:
                    sb.AppendFormat(uIntObj.ToString(MySqlNumberFormat));
                    break;
                case ulong uLongObj:
                    sb.AppendFormat(uLongObj.ToString(MySqlNumberFormat));
                    break;
                case double dblObj:
                    sb.AppendFormat(dblObj.ToString(MySqlNumberFormat));
                    break;
                case decimal decObj:
                    sb.AppendFormat(decObj.ToString(MySqlNumberFormat));
                    break;
                case float floatObj:
                    sb.AppendFormat(floatObj.ToString(MySqlNumberFormat));
                    break;
                case byte byteObj:
                    sb.AppendFormat(byteObj.ToString(MySqlNumberFormat));
                    break;
                case sbyte sByteObj:
                    sb.AppendFormat(sByteObj.ToString(MySqlNumberFormat));
                    break;
                case TimeSpan timeSpanObj:
                {
                    TimeSpan ts = timeSpanObj;

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    sb.AppendFormat(((int)ts.TotalHours).ToString().PadLeft(2, '0'));
                    sb.AppendFormat(":");
                    sb.AppendFormat(ts.Duration().Minutes.ToString().PadLeft(2, '0'));
                    sb.AppendFormat(":");
                    sb.AppendFormat(ts.Duration().Seconds.ToString().PadLeft(2, '0'));

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    break;
                }
                case DateTime dateTimeObj:
                {
                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    sb.AppendFormat(dateTimeObj.ToString("yyyy-MM-dd HH:mm:ss", MySqlDateTimeFormat));

                    if (col.TimeFractionLength > 0)
                    {
                        sb.Append(".");
                        string microsecond = dateTimeObj.ToString("".PadLeft(col.TimeFractionLength, 'f'));
                        sb.Append(microsecond);
                    }

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    break;
                }
                case MySqlDateTime { IsValidDateTime: true } mySqlDateTime:
                {
                    DateTime dtime = mySqlDateTime.GetDateTime();

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    switch (col.MySqlDataType)
                    {
                        case "datetime":
                            sb.AppendFormat(dtime.ToString("yyyy-MM-dd HH:mm:ss", MySqlDateTimeFormat));
                            break;
                        case "date":
                            sb.AppendFormat(dtime.ToString("yyyy-MM-dd", MySqlDateTimeFormat));
                            break;
                        case "time":
                            sb.AppendFormat("{0}:{1}:{2}", mySqlDateTime.Hour, mySqlDateTime.Minute, mySqlDateTime.Second);
                            break;
                        default:
                            sb.AppendFormat(dtime.ToString("yyyy-MM-dd HH:mm:ss", MySqlDateTimeFormat));
                            break;
                    }

                    if (col.TimeFractionLength > 0)
                    {
                        sb.Append(".");
                        sb.Append(mySqlDateTime.Microsecond.ToString().PadLeft(col.TimeFractionLength, '0'));
                    }

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    
                    break;
                }
                case MySqlDateTime:
                {
                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    switch (col.MySqlDataType)
                    {
                        case "datetime":
                            sb.AppendFormat("0000-00-00 00:00:00");
                            break;
                        case "date":
                            sb.AppendFormat("0000-00-00");
                            break;
                        case "time":
                            sb.AppendFormat("00:00:00");
                            break;
                        default:
                            sb.AppendFormat("0000-00-00 00:00:00");
                            break;
                    }

                    if (col.TimeFractionLength > 0)
                        sb.Append(".".PadRight(col.TimeFractionLength, '0'));

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    
                    break;
                }
                case Guid guidObj when col.MySqlDataType == "binary(16)":
                    sb.Append(CryptoExpress.ConvertByteArrayToHexString(guidObj.ToByteArray()));
                    break;
                case Guid when col.MySqlDataType == "char(36)":
                {
                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    sb.Append(ob);

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    
                    break;
                }
                case Guid:
                {
                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");

                    sb.Append(ob);

                    if (wrapStringWithSingleQuote)
                        sb.AppendFormat("'");
                    
                    break;
                }
                default:
                    throw new Exception("Unhandled data type. Current processing data type: " + ob.GetType().ToString() + ". Please report this bug with this message to the development team.");
            }
            return sb.ToString();
        }
    }
}
