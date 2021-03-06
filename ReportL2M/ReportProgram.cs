﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ReportL2M
{
    class ReportProgram
    {
        static void Main(string[] args)
        {
            //"Data Source=localhost\\SQLEXPRESS;Initial Catalog=GRPAFH;Trusted_Connection=Yes;";
            var connection = Properties.Settings.Default.ConnectionString;
            var path1 = Properties.Settings.Default.Path1;
            var path2 = Properties.Settings.Default.Path2;
            foreach (var path in new[] { path1, path2 })
            {
                if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                {
                    File.WriteAllText(Path.Combine(path, "AFHGRPTR2A.HTM"), BuildGrp4Report(1, "Grp4Tr2", "2", connection), Encoding.Default);
                    File.WriteAllText(Path.Combine(path, "AFHGRPTR2B.HTM"), BuildPlantGrp4Report("Grp4Tr2", "2", connection), Encoding.Default);
                    File.WriteAllText(Path.Combine(path, "AFHGRPTR3A.HTM"), BuildGrp4Report(2, "Grp4Tr3", "3", connection), Encoding.Default);
                    File.WriteAllText(Path.Combine(path, "AFHGRPTR3B.HTM"), BuildPlantGrp4Report("Grp4Tr3", "3", connection), Encoding.Default);
                }
                if (path1 == path2) break;
            }
        }

        private static string BuildGrp4Report(byte node, string table, string tube, string connection)
        {
            var hour = (int)FetchRegiser(node, 4, 17);
            if (float.IsNaN(hour))
                hour = 10;
            var day = (int)FetchRegiser(node, 4, 19);
            if (float.IsNaN(day))
                day = 1;

            var Th = FetchRegiser(node, 4, 101, 24, Period.Hours, day, hour);
            var Td = FetchRegiser(node, 4, 149, 21, Period.Days, day, hour);
            var Tm = FetchRegiser(node, 4, 191, 5, Period.Months, day, hour);
            var Mh = FetchRegiser(node, 4, 201, 24, Period.Hours, day, hour);
            var Md = FetchRegiser(node, 4, 249, 21, Period.Days, day, hour);
            var Mm = FetchRegiser(node, 4, 291, 5, Period.Months, day, hour);
            var Vh = FetchRegiser(node, 4, 301, 24, Period.Hours, day, hour);
            var Vd = FetchRegiser(node, 4, 349, 21, Period.Days, day, hour);
            var Vm = FetchRegiser(node, 4, 391, 5, Period.Months, day, hour);
            var Voh = FetchRegiser(node, 4, 401, 24, Period.Hours, day, hour);
            var Vod = FetchRegiser(node, 4, 449, 21, Period.Days, day, hour);
            var Vom = FetchRegiser(node, 4, 491, 5, Period.Months, day, hour);
            var toh = FetchRegiser(node, 4, 501, 24, Period.Hours, day, hour);
            var tod = FetchRegiser(node, 4, 549, 21, Period.Days, day, hour);
            var tom = FetchRegiser(node, 4, 591, 5, Period.Months, day, hour);
            var Pah = FetchRegiser(node, 4, 601, 24, Period.Hours, day, hour);
            var Pad = FetchRegiser(node, 4, 649, 21, Period.Days, day, hour);
            var Pam = FetchRegiser(node, 4, 691, 5, Period.Months, day, hour);

            var server = new SqlServer { Connection = connection };

            foreach (var o in Th) server.ReplaceInto($"{table}H", "T", o.Key, o.Value);
            foreach (var o in Td) server.ReplaceInto($"{table}D", "T", o.Key, o.Value);
            foreach (var o in Tm) server.ReplaceInto($"{table}M", "T", o.Key, o.Value);

            foreach (var o in Mh) server.ReplaceInto($"{table}H", "M", o.Key, o.Value);
            foreach (var o in Md) server.ReplaceInto($"{table}D", "M", o.Key, o.Value);
            foreach (var o in Mm) server.ReplaceInto($"{table}M", "M", o.Key, o.Value);

            foreach (var o in Vh) server.ReplaceInto($"{table}H", "V", o.Key, o.Value);
            foreach (var o in Vd) server.ReplaceInto($"{table}D", "V", o.Key, o.Value);
            foreach (var o in Vm) server.ReplaceInto($"{table}M", "V", o.Key, o.Value);

            foreach (var o in Voh) server.ReplaceInto($"{table}H", "Vo", o.Key, o.Value);
            foreach (var o in Vod) server.ReplaceInto($"{table}D", "Vo", o.Key, o.Value);
            foreach (var o in Vom) server.ReplaceInto($"{table}M", "Vo", o.Key, o.Value);

            foreach (var o in toh) server.ReplaceInto($"{table}H", "to", o.Key, o.Value);
            foreach (var o in tod) server.ReplaceInto($"{table}D", "to", o.Key, o.Value);
            foreach (var o in tom) server.ReplaceInto($"{table}M", "to", o.Key, o.Value);

            foreach (var o in Pah) server.ReplaceInto($"{table}H", "Pa", o.Key, o.Value);
            foreach (var o in Pad) server.ReplaceInto($"{table}D", "Pa", o.Key, o.Value);
            foreach (var o in Pam) server.ReplaceInto($"{table}M", "Pa", o.Key, o.Value);

            //=========================

            server.CalculateFrom($"{table}H", "dd.MM.yyyy", $"Plant{table}D");
            server.CalculateFrom($"Plant{table}D", "01.MM.yyyy", $"Plant{table}M");

            //=========================

            var tmp = Properties.Resources.logikatemplate;
            tmp = tmp.Replace("##title##", $"Поз.FQR-21/{tube}. Архивные данные значений от ГРП-4 в кольцо природного газа от трубопровода №{tube}");
            tmp = tmp.Replace("##HeaderHourTable##", PrepareHeaders("h"));
            tmp = tmp.Replace("##RowsHourTable##", string.Join("\r\n", PrepareRows(server, $"{table}H", 36)));
            tmp = tmp.Replace("##HeaderDayTable##", PrepareHeaders("d"));
            tmp = tmp.Replace("##RowsDayTable##", string.Join("\r\n", PrepareRows(server, $"{table}D", 31)));
            tmp = tmp.Replace("##HeaderMonthTable##", PrepareHeaders("m"));
            tmp = tmp.Replace("##RowsMonthTable##", string.Join("\r\n", PrepareRows(server, $"{table}M", 5)));
            return tmp;
        }

        private static string BuildPlantGrp4Report(string table, string tube, string connection)
        {
            var server = new SqlServer { Connection = connection };
            var tmp = Properties.Resources.logikatemplate;
            tmp = tmp.Replace("##title##", $"Поз.FQR-21/{tube}. Архивные данные значений от ГРП-4 в кольцо природного газа от трубопровода №{tube}");
            tmp = tmp.Replace("##HeaderHourTable##", PrepareHeaders("h"));
            tmp = tmp.Replace("##RowsHourTable##", string.Join("\r\n", PrepareRows(server, $"{table}H", 36)));
            tmp = tmp.Replace("##HeaderDayTable##", PrepareHeaders("d"));
            tmp = tmp.Replace("##RowsDayTable##", string.Join("\r\n", PrepareRows(server, $"Plant{table}D", 31)));
            tmp = tmp.Replace("##HeaderMonthTable##", PrepareHeaders("m"));
            tmp = tmp.Replace("##RowsMonthTable##", string.Join("\r\n", PrepareRows(server, $"Plant{table}M", 5)));
            return tmp;
        }

        private static string PrepareHeaders(string kind)
        {
            var style = "style=\"text-align:left;\" width=\"90\"";
            return $@"<tr>
            <td {style}>to{kind}_t1, ч</td>
            <td {style}>T{kind}_t1, °C</td>
            <td {style}>M{kind}_t1, кг</td>
            <td {style}>V{kind}_t1, м3</td>
            <td {style}>Vo{kind}_t1, м3</td>
            <td {style}>Pa{kind}_t1, МПа</td>
            </tr>";
        }

        private static IEnumerable<string> PrepareRows(SqlServer server, string table, int count)
        {
            var style = "style=\"text-align:right;\"";
            var rows = new List<string>();
            var ds = server.GetRows(table, count);
            if (ds.Tables.Count == 1)
            {
                foreach (var row in ds.Tables[0].Rows.Cast<DataRow>().Reverse())
                {
                    var line = $@"<tr>
                    <td {style}>{row["Snaptime"]:dd.MM.yyyy HH:mm}</td>
                    <td {style}>{row["to"]:0.00}</td>
                    <td {style}>{row["T"]:0.00}</td>
                    <td {style}>{row["M"]:0.00}</td>
                    <td {style}>{row["V"]:0.00}</td>
                    <td {style}>{row["Vo"]:0.00}</td>
                    <td {style}>{row["Pa"]:0.00}</td>
                    </tr>";
                    rows.Add(line);
                }
            }
            return rows;
        }

        private static float FetchRegiser(byte node, byte func, ushort addr)
        {
            var dict = FetchRegiser(node, func, addr, 1);
            return dict.Values.Count == 1 ? dict.Values.First() : float.NaN;
        }

        private static IDictionary<DateTime, float> FetchRegiser(byte node, byte func, ushort addr, int count = 1, Period period = Period.None, int day = 0, int hour = 0)
        {
            var dict = new SortedDictionary<DateTime, float>();
            try
            {
                var IpAddress = IPAddress.Parse("127.0.0.1");
                var remoteEp = new IPEndPoint(IpAddress, 502);
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.SendTimeout = 5000;
                    socket.ReceiveTimeout = 5000;
                    socket.Connect(remoteEp);
                    Thread.Sleep(10);
                    if (socket.Connected)
                    {
                        byte[] buff;
                        int numBytes;
                        var fetchParams = Enumerable.Range(0, count).Select(item => 
                                  new AskParamData() { Node = node, Func = func, RegAddr = addr + item * 2, TypeValue = "float", TypeSwap = "DCBA" });
                        var now = DateTime.Now;
                        var date = now;
                        if (period != Period.None)
                        {
                            if (period == Period.Days && now.Hour < hour)
                                now = now.AddDays(-1);
                            if (period == Period.Months && now.Day == day && now.Hour < hour)
                                now = now.AddMonths(-1);
                            date = new DateTime(now.Year, now.Month, period == Period.Days || period == Period.Hours ? now.Day : day, period == Period.Hours ? now.Hour : hour, 0, 0);
                        }
                        foreach (var item in fetchParams)
                        {
                            socket.Send(PrepareFetchParam(item.Node, item.Func, item.RegAddr, item.TypeValue));
                            Thread.Sleep(10);
                            buff = new byte[8192];
                            numBytes = socket.Receive(buff);
                            if (numBytes > 0)
                            {
                                var answer = CleanAnswer(buff);
                                if (CheckAnswer(answer, item.Node, item.Func, item.TypeValue))
                                {
                                    var result = EncodeFetchAnswer(answer, item.Node, item.Func, item.RegAddr, item.TypeValue, item.TypeSwap, item.UnitValue);
                                    dict.Add(date, float.Parse(result.Value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US")));
                                    switch (period)
                                    {
                                        case Period.Hours:
                                            date = date.AddHours(-1);
                                            break;
                                        case Period.Days:
                                            date = date.AddDays(-1);
                                            break;
                                        case Period.Months:
                                            date = date.AddMonths(-1);
                                            break;
                                    }
                                }
                            }
                        }
                        socket.Disconnect(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            dict.Reverse();
            return dict;
        }

        private static byte[] Swap(byte[] buff, int startIndex, string typeSwap)
        {
            var list = buff.Skip(startIndex).ToArray();
            if (list.Length == 2)
            {
                switch (typeSwap)
                {
                    case "AB":
                        return new byte[] { list[0], list[1] };
                    case "BA":
                        return new byte[] { list[1], list[0] };
                    default:
                        return list;
                }
            }
            else if (list.Length == 4)
            {
                switch (typeSwap)
                {
                    case "ABCD":
                        return new byte[] { list[0], list[1], list[2], list[3] };
                    case "CDAB":
                        return new byte[] { list[2], list[3], list[0], list[1] };
                    case "BADC":
                        return new byte[] { list[1], list[0], list[3], list[2] };
                    case "DCBA":
                        return new byte[] { list[3], list[2], list[1], list[0] };
                    default:
                        return list;
                }
            }
            else if (list.Length == 8)
            {
                switch (typeSwap)
                {
                    case "ABCDEFGH":
                        return new byte[] { list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7] };
                    case "GHEFCDAB":
                        return new byte[] { list[6], list[7], list[4], list[5], list[2], list[3], list[0], list[1] };
                    case "BADCFEHG":
                        return new byte[] { list[1], list[0], list[3], list[2], list[5], list[4], list[7], list[6] };
                    case "HGFEDCBA":
                        return new byte[] { list[7], list[6], list[5], list[4], list[3], list[2], list[1], list[0] };
                    default:
                        return list;
                }
            }
            else
                return list;
        }

        private static AnswerData EncodeFetchAnswer(byte[] answer, byte node, byte func, int regAddr, string typeValue, string typeSwap, string unitValue)
        {
            var dataset = new List<byte>(); // содержит данные ответа
            string value = string.Empty;
            switch (typeValue)
            {
                case "uint16":
                    if (answer.Length == 5)
                    {
                        var data = BitConverter.ToUInt16(Swap(answer, 3, typeSwap), 0);
                        if (unitValue == "bits")
                        {
                            var sb = new StringBuilder();
                            for (var i = 0; i < 16; i++)
                            {
                                var bc = data & 0x01;
                                if (bc > 0)
                                    sb.Insert(0, "1");
                                else
                                    sb.Insert(0, "0");
                                data = (UInt16)(data >> 1);
                                if (i % 4 == 3)
                                    sb.Insert(0, " ");
                            }
                            value = sb.ToString().Trim();
                        }
                        else
                            value = data.ToString(CultureInfo.GetCultureInfo("en-US"));
                    }
                    break;
                case "uint32":
                    if (answer.Length == 7)
                    {
                        var data = BitConverter.ToUInt32(Swap(answer, 3, typeSwap), 0);
                        if (unitValue == "UTC")
                        {
                            var dateTime = ConvertFromUnixTimestamp(data).ToLocalTime();
                            value = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.GetCultureInfo("en-US"));
                        }
                        else
                            value = data.ToString(CultureInfo.GetCultureInfo("en-US"));
                    }
                    break;
                case "float":
                    if (answer.Length == 7)
                    {
                        var data = BitConverter.ToSingle(Swap(answer, 3, typeSwap), 0);
                        value = data.ToString("0.####", CultureInfo.GetCultureInfo("en-US"));
                    }
                    break;
                case "double":
                    if (answer.Length == 11)
                    {
                        var data = BitConverter.ToDouble(Swap(answer, 3, typeSwap), 0);
                        value = data.ToString("0.####", CultureInfo.GetCultureInfo("en-US"));
                    }
                    break;
            }
            return new AnswerData()
            {
                Node = node,
                Func = func,
                RegAddr = regAddr,
                Value = value,
                Unit = unitValue
            };
        }

        private static DateTime ConvertFromUnixTimestamp(uint timestamp)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }

        private static bool CheckAnswer(byte[] answer, byte node, byte func, string typeValue)
        {
            var datacount = DataLength(typeValue);
            if (datacount * 2 + 3 == answer.Length)
            {
                if (answer[0] == node && answer[1] == func && datacount * 2 == answer[2])
                    return true;
            }
            return false;
        }

        private static byte[] CleanAnswer(IEnumerable<byte> receivedBytes)
        {
            var source = new List<byte>();
            var length = 0;
            var n = 0;
            foreach (var b in receivedBytes)
            {
                if (n == 5)
                    length = b;
                else if (n > 5 && length > 0)
                {
                    source.Add(b);
                    if (source.Count == length)
                        break;
                }
                n++;
            }
            return source.ToArray();
        }

        private static byte[] PrepareFetchParam(byte node, byte func, int regAddr, string typeValue)
        {
            var datacount = DataLength(typeValue);
            var addr = regAddr - 1;
            return EncodeData(0, 0, 0, 0, 0, 6, (byte)node, (byte)(func),
                                       (byte)(addr >> 8), (byte)(addr & 0xff),
                                       (byte)(datacount >> 8), (byte)(datacount & 0xff));
        }

        private static byte[] EncodeData(params byte[] list)
        {
            var result = new byte[list.Length];
            for (var i = 0; i < list.Length; i++) result[i] = list[i];
            return result;
        }

        private static int DataLength(string typeValue)
        {
            int datacount = 1; // запрашиваем количество регистров
            switch (typeValue)
            {
                case "uint16":
                    datacount = 1;
                    break;
                case "uint32":
                case "float":
                    datacount = 2;
                    break;
                case "double":
                    datacount = 4;
                    break;
            }
            return datacount;
        }

    }

    public enum Period
    {
        None,
        Hours,
        Days,
        Months
    }

    public class AskParamData
    {
        public byte Node { get; set; }         // байт адреса прибора
        public byte Func { get; set; }         // номер функции Modbus
        public int RegAddr { get; set; }       // номер регистра
        public string TypeValue { get; set; }  // тип переменной
        public string TypeSwap { get; set; }   // тип перестановки
        public string UnitValue { get; set; }  // единица измерения
        public string ParamName { get; set; }
        public string LastValue { get; set; }
        public bool ExistsInSqlTable { get; set; }
    }

    public class AnswerData
    {
        public byte Node { get; set; }         // байт адреса прибора
        public byte Func { get; set; }         // номер функции Modbus
        public int RegAddr { get; set; }       // номер регистра
        public string Value { get; set; }
        public string Unit { get; set; }

        public override string ToString()
        {
            return $"{Node} {Func} {RegAddr} {Value} {Unit}";
        }
    }
}
