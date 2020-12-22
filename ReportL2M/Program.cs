using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportL2M
{
    class Program
    {
        static void Main(string[] args)
        {
            var tmp = Properties.Resources.logikatemplate;
            tmp = tmp.Replace("##title##", "Поз.FQR-21/3. Архивные данные значений от ГРП-4 в кольцо природного газа от трубопровода №3");

            var style = "style=\"text-align:left;\" width=\"90\"";

            tmp = tmp.Replace("##HeaderHourTable##",
$@"<tr>
<td {style}>Toh1_t1, ч</td>
<td {style}>Th1_t1, °C</td>
<td {style}>Mh1_t1, кг</td>
<td {style}>Vh1_t1, м3</td>
<td {style}>Voh1_t1, м3</td>
</tr>");
            style = "style=\"text-align:right;\"";
            var rows = new List<string>();

            for (var i = 0; i < 10; i++)
            {
                var row = $@"<tr>
<td {style}>21.12.2020 00:00</td>
<td {style}>1</td>
<td {style}>3.3945</td>
<td {style}>6524.87</td>
<td {style}>9227.65</td>
<td {style}>1490.21</td>
</tr>";
                rows.Add(row);
            }

            tmp = tmp.Replace("##RowsHourTable##", string.Join("\r\n", rows));


            File.WriteAllText("report.htm", tmp, Encoding.Default);
        }

    }
}
