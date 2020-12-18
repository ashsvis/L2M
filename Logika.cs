using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace L2M
{
    public static class Logika
    {
        public static void FetchParameter(Socket socket, byte dad, byte sad, int channel, int parameter,
            byte nodeAddr, ModbusTable modbusTable, ushort startAddr, string dataFormat, int answerWait)
        {
            socket.Send(PrepareFetchParam(dad, sad, channel, parameter));
            Thread.Sleep(answerWait);
            var buff = new byte[8192];
            var numBytes = socket.Receive(buff);
            if (numBytes > 0)
            {
                var answer = CleanAnswer(buff);
                if (CheckAnswer(answer))
                {
                    var result = EncodeFetchAnswer(answer);
                    if (result.Dad == sad && result.Sad == dad && result.Fnc == 3 &&
                        result.Channel == channel && result.Parameter == parameter)
                    {
                        Console.SetCursorPosition(0, startAddr + 5);
                        Console.Write($"{result.Value} {result.Unit}");

                        if (dataFormat == "IEEEFP" &&
                            float.TryParse(result.Value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US"), out float floatValue))
                        {
                            ushort addr = startAddr;
                            var n = 0;
                            var bytes = BitConverter.GetBytes(floatValue);
                            Array.Reverse(bytes);
                            for (ushort i = 0; i < 2; i++)
                            {
                                var regAddr = Modbus.ModifyToModbusRegisterAddress(addr, modbusTable);
                                ushort value = BitConverter.ToUInt16(bytes, n);
                                Modbus.SetRegisterValue(nodeAddr, regAddr, value);
                                n = n + 2;  // коррекция позиции смещения в принятых данных для записи
                                addr += 1;
                            }
                        }
                        else
                        if (dataFormat == "S32B" &&
                            int.TryParse(result.Value, NumberStyles.Integer, CultureInfo.GetCultureInfo("en-US"), out int intValue))
                        {
                            ushort addr = startAddr;
                            var n = 0;
                            var bytes = BitConverter.GetBytes(intValue);
                            //Array.Reverse(bytes);  <--- не делать реверс для uint
                            for (ushort i = 0; i < 2; i++)
                            {
                                var regAddr = Modbus.ModifyToModbusRegisterAddress(addr, modbusTable);
                                ushort value = BitConverter.ToUInt16(bytes, n);
                                Modbus.SetRegisterValue(nodeAddr, regAddr, Modbus.Swap(value)); // <--- Swap() для uint
                                n = n + 2;  // коррекция позиции смещения в принятых данных для записи
                                addr += 1;
                            }
                        }

                    }
                }
                else
                    throw new Exception($"Logika DAD:{dad} {channel}.{parameter} checksumm error");
            }
        }

        static AnswerData EncodeFetchAnswer(IEnumerable<byte> buff)
        {
            var arr = Unstuff(buff);
            // разборка телеграммы
            // [0] - SOH; [1] - DAD; [2] - SAD; [3] - ISI; [4] - FNC
            var n = 0;
            byte dad = 0;
            byte sad = 0;
            byte fnc = 0;
            var stx = false;
            var etx = false;
            var dataset = new List<byte>(); // содержит данные ответа
            while (n < arr.Length)
            {
                switch (n)
                {
                    case 1: dad = arr[n]; break;
                    case 2: sad = arr[n]; break;
                    case 4: fnc = arr[n]; break; // содержит 0x03 при чтении параметров
                    default:
                        if (n > 4)
                        {
                            if (!etx && (arr[n] == ETX)) etx = true;
                            if (stx && !etx) dataset.Add(arr[n]);
                            if (!stx && (arr[n] == STX)) stx = true;
                        }
                        break;
                }
                n++;
            }
            // разбор блока данных
            var channel = new byte[] { };
            var param = new byte[] { };
            var value = new byte[] { };
            var eu = new byte[] { };
            var time = new byte[] { };
            var addr = true;
            var index = 0;
            n = 0;
            var data = new List<byte>();
            while (n < dataset.Count)
            {
                if ((dataset[n] == HT) || (dataset[n] == FF))
                {
                    switch (index)
                    {
                        case 1:
                            if (addr)
                                channel = data.ToArray();
                            else
                                value = data.ToArray();
                            break;
                        case 2:
                            if (addr)
                                param = data.ToArray();
                            else
                                eu = data.ToArray();
                            break;
                        case 3:
                            if (!addr)
                                time = data.ToArray();
                            break;
                    }
                    data.Clear();
                    index++;
                }
                else
                    data.Add(dataset[n]);
                if (addr && (dataset[n] == FF))
                {
                    addr = false;
                    index = 0;
                }
                n++;
            }
            int chano = -1;
            int.TryParse(cp866unicode.UnicodeString(channel), out chano);
            int parno = -1;
            int.TryParse(cp866unicode.UnicodeString(param), out parno);
            var unit = cp866unicode.UnicodeString(eu);
            if (unit == "'C") unit = "°C";

            return new AnswerData()
            {
                Dad = dad,
                Sad = sad,
                Fnc = fnc,
                Channel = chano,
                Parameter = parno,
                Value = cp866unicode.UnicodeString(value),
                Unit = unit,
                Time = cp866unicode.UnicodeString(time)
            };
        }

        static byte[] Unstuff(IEnumerable<byte> buff)
        {
            // анти-стаффинг
            var arr = new List<byte>();
            var dle = false;
            foreach (var b in buff)
            {
                if ((b != DLE) || (b == DLE) && dle) arr.Add(b);
                dle = b == DLE;
            }
            return arr.ToArray();
        }

        /// <summary>
        /// Очистка телеграммы от мусора
        /// </summary>
        /// <param name="receivedBytes">Сырые данные ответа</param>
        /// <returns>Очищенные данные</returns>
        static byte[] CleanAnswer(IEnumerable<byte> receivedBytes)
        {
            var source = new List<byte>();
            var soh = false;
            var dle = false;
            var stx = false;
            var etx = false;
            var crclen = 2;
            foreach (var b in receivedBytes)
            {
                if (soh) source.Add(b);
                if ((b == ETX) && dle && stx && !etx) { etx = true; }
                if ((b == STX) && dle && !stx) { stx = true; }
                if ((b == SOH) && dle && !soh) { soh = true; source.Add(DLE); source.Add(SOH); }
                dle = b == DLE;
                if (stx && etx) crclen--;
                if (crclen < 0) break;
            }
            return source.ToArray();
        }

        static bool CheckAnswer(byte[] cleanBytes)
        {
            if (cleanBytes.Length < 3) return false;
            // проверка КС
            var test = new byte[cleanBytes.Length - 2];
            Array.Copy(cleanBytes, 2, test, 0, cleanBytes.Length - 2);
            var crc = CrCode(test);
            return crc == 0;
        }

        const byte DLE = 0x10;
        const byte SOH = 0x01; // начало заголовка
        const byte ISI = 0x1f; // указатель кода функции
        const byte STX = 0x02; // начало тела сообщения
        const byte ETX = 0x03; // конец тела сообщения
        const byte HT = 0x09;
        const byte FF = 0x0c;

        /// <summary>
        /// Подготовка сообщения для запроса значения параметра
        /// </summary>
        /// <param name="dad">байт адреса приёмника</param>
        /// <param name="sad">байт адреса источника</param>
        /// <param name="channel">номер канала прибора</param>
        /// <param name="parameter">номер параметра прибора</param>
        /// <returns></returns>
        static byte[] PrepareFetchParam(byte dad, byte sad, int channel, int parameter)
        {
            byte FNC = 0x1d; // код функции для запроса значения параметра
            var list = new List<byte> { DLE, SOH, dad, sad, DLE, ISI, FNC, DLE, STX };
            list.Add(HT);
            list.AddRange(cp866unicode.OemString(channel.ToString()));
            list.Add(HT);
            list.AddRange(cp866unicode.OemString(parameter.ToString()));
            list.Add(FF);
            list.Add(DLE); list.Add(ETX);
            // контрольная сумма
            byte[] crcbuff = list.ToArray();
            var arg = new byte[crcbuff.Length - 2];
            Array.Copy(crcbuff, 2, arg, 0, crcbuff.Length - 2);
            var crc = CrCode(arg);
            list.Add((byte)(crc >> 8)); // high crc parth
            list.Add((byte)(crc & 0xff)); // low crc parth
            return list.ToArray();
        }

        static int CrCode(IEnumerable<byte> msg)
        {
            var crc = 0;
            foreach (var b in msg)
            {
                crc = crc ^ b << 8;
                for (var j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) > 0)
                        crc = (crc << 1) ^ 0x1021;
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }

    }
}
