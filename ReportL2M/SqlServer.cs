using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

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

        public void CalculateFrom(string table, string mask, string result)
        {
            using (var con = new SqlConnection(Connection))
            {
                var sql = $"SELECT * FROM [{table}] ORDER BY [Snaptime] ASC";
                using (var da = new SqlDataAdapter(sql, con))
                {
                    var ds = new DataSet();
                    try
                    {
                        da.Fill(ds, table);
                        if (ds.Tables.Count > 0)
                        {
                            Filter filter = DateFilter;

                            foreach (var group in ds.Tables[0].Rows.Cast<DataRow>().GroupBy(item => filter((DateTime)item["Snaptime"], mask)))
                            {
                                var to = group.Sum(item => Convert.ToSingle(item["to"]));
                                var T = group.Average(item => Convert.ToSingle(item["T"]));
                                var M = group.Sum(item => Convert.ToSingle(item["M"]));
                                var V = group.Sum(item => Convert.ToSingle(item["V"]));
                                var Vo = group.Sum(item => Convert.ToSingle(item["Vo"]));
                                var Pa = group.Average(item => Convert.ToSingle(item["Pa"]));

                                ReplaceInto(result, "to", group.Key, to);
                                ReplaceInto(result, "T", group.Key, T);
                                ReplaceInto(result, "M", group.Key, M);
                                ReplaceInto(result, "V", group.Key, V);
                                ReplaceInto(result, "Vo", group.Key, Vo);
                                ReplaceInto(result, "Pa", group.Key, Pa);
                            }
                        }
                        LastError = "";
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                    }
                }
            }
        }

        private DateTime DateFilter(DateTime dateTime, string mask)
        {
            return DateTime.Parse(dateTime.ToString(mask));
        }

        public delegate DateTime Filter(DateTime dateTime, string mask);

    }
}
