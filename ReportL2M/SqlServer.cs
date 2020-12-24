using System;
using System.Data;
using System.Data.SqlClient;

namespace ReportL2M
{
    /// <summary>
    /// Класс для работы с базой данных SQL сервера
    /// </summary>
    public class SqlServer
    {
        public string Connection { get; set; } = string.Empty; // строка подключения
        public string LastError { get; set; } = string.Empty; // последняя ошибка

        public bool ReplaceInto(string table, string field, DateTime time, float value)
        {
            using (var con = new SqlConnection(Connection))
            {
                try
                {
                    con.Open();
                    var found = false;
                    using (SqlCommand cmd = new SqlCommand($"SELECT COUNT(*) FROM[{table}] WHERE [Snaptime]=@Snaptime", con))
                    {
                        cmd.Parameters.AddWithValue("@Snaptime", time);
                        found = (int)cmd.ExecuteScalar() > 0;
                    }
                    var sql = found
                        ? $"UPDATE [{table}] SET [{field}]=@Value WHERE [Snaptime]=@Snaptime"
                        : $"INSERT INTO [{table}] ([Snaptime], [{field}]) VALUES(@Snaptime, @Value)";
                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Value", value);
                        cmd.Parameters.AddWithValue("@Snaptime", time);
                        cmd.ExecuteNonQuery();
                    }
                    con.Close();
                    LastError = "";
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    return false;
                }
            }
        }

        public DataSet GetRows(string table, int count)
        {
            using (var con = new SqlConnection(Connection))
            {
                var sql = $"SELECT TOP {count} * FROM [{table}] ORDER BY [Snaptime] DESC";
                using (var da = new SqlDataAdapter(sql, con))
                {
                    var ds = new DataSet();
                    try
                    {
                        da.Fill(ds, table);
                        LastError = "";
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                    }
                    return ds;
                }
            }
        }

    }
}
