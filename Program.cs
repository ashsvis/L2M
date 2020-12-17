using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Linq;

namespace L2M
{
    class Program
    {
        private static readonly ushort[,] registers = new ushort[247, 50000];

        private static readonly object locker = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("MODBUS listening service loaded.");
            Console.WriteLine("Ver. 0.3\n");
            // чтение конфигурационного файла
            var xdoc = XDocument.Load("L2M.xml");
            XElement listenTcp = xdoc.Element("Config").Element("ListenTcp");
            XElement element = listenTcp.Element("IpPort");
            if (element == null || !int.TryParse(element.Value, out int ipPort))
                ipPort = 502;
            element = listenTcp.Element("SendTimeout");
            if (element == null || !int.TryParse(element.Value, out int sendTimeout))
                sendTimeout = 5000;
            element = listenTcp.Element("ReceiveTimeout");
            if (element == null || !int.TryParse(element.Value, out int receiveTimeout))
                receiveTimeout = 5000;
            // запуск потока для прослушивания запосов от устройства по протоколу Modbus Tcp
            var listener = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            listener.DoWork += ModbusListener_DoWork;
            listener.ProgressChanged += ModbusListener_ProgressChanged;
            var tcptuning = new TcpTuning
            {
                Address = IPAddress.Any,
                Port = ipPort,
                SendTimeout = sendTimeout,
                ReceiveTimeout = receiveTimeout,
            };
            listener.RunWorkerAsync(tcptuning);
            // чтение параметров для опрашивающих потоков
            foreach (XElement fetchingTcp in xdoc.Element("Config").Element("Fetching").Elements("ChannelTcp"))
            {
                var good = true;
                element = fetchingTcp.Element("IpAddress");
                IPAddress ipAddress = null;
                if (element == null || !IPAddress.TryParse(element.Value, out ipAddress))
                    good = false;
                element = fetchingTcp.Element("IpPort");
                if (element == null || !int.TryParse(element.Value, out ipPort))
                    good = false;
                element = listenTcp.Element("SendTimeout");
                if (element == null || !int.TryParse(element.Value, out sendTimeout))
                    good = false;
                element = listenTcp.Element("ReceiveTimeout");
                if (element == null || !int.TryParse(element.Value, out receiveTimeout))
                    good = false;
                if (good)
                {
                    var parameters = new List<RequestData>();
                    byte dad = 0, sad = 0, nodeAddr = 0;
                    int channel = 0, parameter = 0;
                    ModbusTable modbusTable = ModbusTable.None;
                    ushort startAddr = 0;
                    string dataFormat = "";
                    foreach (var item in fetchingTcp.Element("Runtime").Elements("LogikaItem"))
                    {
                        element = item.Element("Dad");
                        if (element == null || !byte.TryParse(element.Value, out dad))
                            good = false;
                        element = item.Element("Sad");
                        if (element == null || !byte.TryParse(element.Value, out sad))
                            good = false;
                        element = item.Element("Channel");
                        if (element == null || !int.TryParse(element.Value, out channel))
                            good = false;
                        element = item.Element("Parameter");
                        if (element == null || !int.TryParse(element.Value, out parameter))
                            good = false;
                        element = item.Element("ModbusNode");
                        if (element == null || !byte.TryParse(element.Value, out nodeAddr))
                            good = false;
                        element = item.Element("DataFormat");
                        if (element != null)
                            dataFormat = element.Value;
                        else
                            good = false;
                        element = item.Element("InputRegister");
                        if (element != null)
                        {
                            modbusTable = ModbusTable.Inputs;
                            if (!ushort.TryParse(element.Value, out startAddr))
                                good = false;
                        }
                        else
                        {
                            element = item.Element("HoldingRegister");
                            if (element != null)
                            {
                                modbusTable = ModbusTable.Holdings;
                                if (!ushort.TryParse(element.Value, out startAddr))
                                    good = false;
                            }
                            else
                                good = false;
                        }
                        if (good)
                        {
                            parameters.Add(new RequestData()
                            {
                                Dad = dad,
                                Sad = sad,
                                Channel = channel,
                                Parameter = parameter,
                                NodeAddr = nodeAddr,
                                StartAddr = startAddr,
                                ModbusTable = modbusTable,
                                FormatData = dataFormat
                            });
                        }
                    }

                    var worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
                    worker.DoWork += Worker_DoWork;
                    worker.ProgressChanged += Worker_ProgressChanged;
                    tcptuning = new TcpTuning
                    {
                        Address = ipAddress,
                        Port = ipPort,
                        SendTimeout = sendTimeout,
                        ReceiveTimeout = receiveTimeout,
                        Parameters = parameters,
                        //FetchArchives = fetchArchives
                    };
                    worker.RunWorkerAsync(tcptuning);
                }
            }

            Console.WriteLine("Press any key for exit...");
            Console.ReadKey();
        }

        private static void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            if (!(e.Argument is TcpTuning parameters)) return;
            var lastsecond = DateTime.Now.Second;
            var remoteEp = new IPEndPoint(parameters.Address, parameters.Port);
            while (!worker.CancellationPending)
            {
                var dt = DateTime.Now;
                if (lastsecond == dt.Second) continue;
                lastsecond = dt.Second;
                // прошла секунда
                try
                {
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        socket.SendTimeout = parameters.SendTimeout;
                        socket.ReceiveTimeout = parameters.ReceiveTimeout;
                        socket.Connect(remoteEp);
                        Thread.Sleep(500);
                        if (socket.Connected)
                        {
                            foreach (var p in parameters.Parameters)
                            {
                                FetchLogikaParameter(socket, p.Dad, p.Sad, p.Channel, p.Parameter, p.NodeAddr, p.ModbusTable, p.StartAddr, p.FormatData);
                            }
                            //FetchLogikaParameter(socket, 1, 3, 1, 160, 1, 0, typeof(float));
                            //FetchLogikaParameter(socket, 1, 3, 0, 8, 1, 2, typeof(uint));
                            //FetchLogikaParameter(socket, 1, 3, 1, 162, 1, 4, typeof(float));
                            //FetchLogikaParameter(socket, 1, 3, 1, 163, 1, 6, typeof(float));
                        }
                    }
                }
                catch (Exception ex)
                {
                    worker.ReportProgress(0, ex.Message);
                }
            }
        }

        private static void FetchLogikaParameter(Socket socket, byte dad, byte sad, int channel, int parameter, 
            byte nodeAddr, ModbusTable modbusTable, ushort startAddr, string dataFormat)
        {
            socket.Send(PrepareFetchParam(dad, sad, channel, parameter));
            Thread.Sleep(500);
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
                        if (dataFormat == "IEEEFP" && 
                            float.TryParse(result.Value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US"), out float floatValue))
                        {
                            ushort addr = startAddr;
                            var n = 0;
                            var bytes = BitConverter.GetBytes(floatValue);
                            Array.Reverse(bytes);
                            for (ushort i = 0; i < 2; i++)
                            {
                                var regAddr = ModifyToModbusRegisterAddress(addr, modbusTable);
                                ushort value = BitConverter.ToUInt16(bytes, n);
                                SetRegisterValue(nodeAddr, regAddr, value);
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
                                var regAddr = ModifyToModbusRegisterAddress(addr, modbusTable);
                                ushort value = BitConverter.ToUInt16(bytes, n);
                                SetRegisterValue(nodeAddr, regAddr, Swap(value)); // <--- Swap() для uint
                                n = n + 2;  // коррекция позиции смещения в принятых данных для записи
                                addr += 1;
                            }
                        }

                    }
                }
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

        private static void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Console.WriteLine($"{e.UserState}");
        }

        private static void ModbusListener_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            if (!(e.Argument is TcpTuning parameters)) return;
            //
            var listener = new TcpListener(IPAddress.Any, parameters.Port)
            {
                Server = { SendTimeout = parameters.SendTimeout, ReceiveTimeout = parameters.ReceiveTimeout }
            };
            //
            do
            {
                Thread.Sleep(parameters.WaitForConnect);
                try
                {
                    listener.Start(10);
                    // Buffer for reading data
                    var bytes = new byte[256];

                    while (!listener.Pending())
                    {
                        Thread.Sleep(1);
                        if (!worker.CancellationPending) continue;
                        listener.Stop();
                        //TranslateChannelMessage(parameters.ChannelId, "Not listening");
                        worker.ReportProgress(0, "Not listening");
                        return;
                    }
                    var clientData = listener.AcceptTcpClient();
                    // создаем отдельный поток для каждого подключения клиента
                    ThreadPool.QueueUserWorkItem(arg =>
                    {
                        try
                        {
                            // Get a stream object for reading and writing
                            var stream = clientData.GetStream();
                            int count;
                            // Loop to receive all the data sent by the client.
                            while ((count = stream.Read(bytes, 0, bytes.Length)) != 0)
                            {
                                Thread.Sleep(1);
                                var list = new List<string>();
                                for (var i = 0; i < count; i++) list.Add(string.Format("{0}", bytes[i]));
                                
                                //worker.ReportProgress(0, "Q:" + string.Join(",", list));

                                if (count < 6) continue;
                                var header1 = Convert.ToUInt16(bytes[0] * 256 + bytes[1]);
                                var header2 = Convert.ToUInt16(bytes[2] * 256 + bytes[3]);
                                var packetLen = Convert.ToUInt16(bytes[4] * 256 + bytes[5]);
                                if (count != packetLen + 6) continue;
                                var nodeAddr = bytes[6];
                                var funcCode = bytes[7];
                                var startAddr = Convert.ToUInt16(bytes[8] * 256 + bytes[9]);
                                var regCount = Convert.ToUInt16(bytes[10] * 256 + bytes[11]);
                                var singleValue = Convert.ToUInt16(bytes[10] * 256 + bytes[11]);
                                List<byte> answer;
                                byte bytesCount;
                                byte[] msg;
                                switch (funcCode)
                                {
                                    case 3: // - read holding registers
                                    case 4: // - read input registers
                                        answer = new List<byte>();
                                        answer.AddRange(BitConverter.GetBytes(Swap(header1)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(header2)));
                                        bytesCount = Convert.ToByte(regCount * 2);
                                        packetLen = Convert.ToUInt16(bytesCount + 3);
                                        answer.AddRange(BitConverter.GetBytes(Swap(packetLen)));
                                        answer.Add(nodeAddr);
                                        answer.Add(funcCode);
                                        answer.Add(bytesCount);
                                        //
                                        //worker.ReportProgress(nodeAddr, $"node:{nodeAddr} func:{funcCode} addr:{startAddr} count:{regCount}");

                                        for (ushort i = 0; i < regCount; i++)
                                        {
                                            var regAddr = ModifyToModbusRegisterAddress((ushort)(i + startAddr), (ModbusTable)funcCode);
                                            ushort value = GetRegisterValue(nodeAddr, regAddr);
                                            answer.AddRange(BitConverter.GetBytes(value));
                                        }

                                        msg = answer.ToArray();
                                        stream.Write(msg, 0, msg.Length);
                                        break;
                                    case 6: // write one register
                                        SetRegisterValue(nodeAddr, ModifyToModbusRegisterAddress(startAddr, ModbusTable.Holdings), singleValue);
                                        //-------------------
                                        answer = new List<byte>();
                                        answer.AddRange(BitConverter.GetBytes(Swap(header1)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(header2)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(6)));
                                        answer.Add(nodeAddr);
                                        answer.Add(funcCode);
                                        answer.AddRange(BitConverter.GetBytes(Swap(startAddr)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(regCount)));
                                        msg = answer.ToArray();
                                        stream.Write(msg, 0, msg.Length);
                                        break;
                                    case 16: // write several registers
                                        var n = 13;
                                        ushort addr = startAddr;
                                        for (ushort i = 0; i < regCount; i++)
                                        {
                                            var regAddr = ModifyToModbusRegisterAddress(addr, ModbusTable.Holdings);
                                            ushort value = BitConverter.ToUInt16(bytes, n);
                                            SetRegisterValue(nodeAddr, regAddr, value);
                                            n = n + 2;  // коррекция позиции смещения в принятых данных для записи
                                            addr += 1;
                                        }
                                        //-------------------
                                        answer = new List<byte>();
                                        answer.AddRange(BitConverter.GetBytes(Swap(header1)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(header2)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(6)));
                                        answer.Add(nodeAddr);
                                        answer.Add(funcCode);
                                        answer.AddRange(BitConverter.GetBytes(Swap(startAddr)));
                                        answer.AddRange(BitConverter.GetBytes(Swap(regCount)));
                                        msg = answer.ToArray();
                                        stream.Write(msg, 0, msg.Length);
                                        break;
                                }
                            }
                            // Shutdown and end connection
                            clientData.Close();
                        }
                        catch (Exception ex)
                        {
                            if (!worker.CancellationPending)
                                
                                worker.ReportProgress(0, ex.Message);
                        }
                    });
                }
                catch (SocketException ex)
                {
                    if (!worker.CancellationPending)
                        
                        worker.ReportProgress(0, ex.Message);
                    break;
                }
            } while (!worker.CancellationPending);
            listener.Stop();

        }

        private static ushort GetRegisterValue(byte node, ushort index)
        {
            lock (locker)
            {
                return registers[node - 1, index - 1];
            }
        }

        private static void SetRegisterValue(byte node, ushort index, ushort value)
        {
            lock (locker)
            {
                registers[node - 1, index - 1] = value;
            }
        }

        private static ushort Swap(ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            var buff = bytes[0];
            bytes[0] = bytes[1];
            bytes[1] = buff;
            return BitConverter.ToUInt16(bytes, 0);
        }

        private static ushort ModifyToModbusRegisterAddress(ushort startAddr, ModbusTable funcCode)
        {
            switch (funcCode)
            {
                case ModbusTable.Coils:
                    return Convert.ToUInt16(1 + startAddr);       // coils
                case ModbusTable.Contacts:
                    return Convert.ToUInt16(10001 + startAddr);   // contacts
                case ModbusTable.Holdings:
                    return Convert.ToUInt16(40001 + startAddr);   // holdings
                case ModbusTable.Inputs:
                    return Convert.ToUInt16(30001 + startAddr);   // inputs
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Отображаем значения регистров в консоли программы
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ModbusListener_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //if (e.ProgressPercentage != 0)
            //{
            //    Console.Clear();
            //    var top = 0;
            //    var node = (byte)e.ProgressPercentage;
            //    lock (locker)
            //    {
            //        // диапазон 4хххх - для holding регистров
            //        for (var i = 40000; i < 40010; i++)
            //        {
            //            Console.SetCursorPosition(0, top++);
            //            Console.Write(Swap(registers[node - 1, i]));
            //        }
            //    }
            //    Console.SetCursorPosition(0, ++top);
            //    Console.Write($"{e.UserState}");
            //}
            Console.WriteLine($"{e.UserState}");
        }
    }

    public class RequestData
    {
        public byte Dad { get; set; }
        public byte Sad { get; set; }
        public int Channel { get; set; }
        public int Parameter { get; set; }
        public byte NodeAddr { get; set; }
        public ModbusTable ModbusTable { get; set; }
        public ushort StartAddr { get; set; }
        public string FormatData { get; set; }
    }

    public enum ModbusTable
    {
        None = 0,
        Coils = 1,
        Contacts = 2,
        Holdings = 3,
        Inputs = 4
    }

    public class TcpTuning
    {
        public Guid ChannelId { get; set; }
        public IPAddress Address { get; set; } = new IPAddress(new byte[] { 127, 0, 0, 1 });
        public int Port { get; set; } = 502;
        public int SendTimeout { get; set; } = 5000;
        public int ReceiveTimeout { get; set; } = 5000;
        public int WaitForConnect { get; set; } = 50;
        public int WaitForAnswer { get; set; } = 200;
        public IEnumerable<RequestData> Parameters { get; set; } = new List<RequestData>();
    }

    public static class cp866unicode
    {
        public static byte[] OemString(string unicodestring)
        {
            List<byte> list = new List<byte>();
            char[] values = unicodestring.ToCharArray();
            foreach (char ch in values) list.Add(OemChar(ch));
            return list.ToArray();
        }

        public static string UnicodeString(byte[] oemstring)
        {
            char[] result = new char[oemstring.Length];
            for (int i = 0; i < oemstring.Length; i++)
                result[i] = UnicodeChar(oemstring[i]);
            return new string(result);
        }

        private static byte OemChar(char unicodechar)
        {
            for (byte b = 0; b <= 255; b++)
                if (UnicodeChar(b) == unicodechar) return b;
            return 0;
        }

        private static char UnicodeChar(byte oemchar)
        {
            switch (oemchar)
            {
                /*
                #
                #    Name:     cp866_DOSCyrillicRussian to Unicode table
                #    Unicode version: 2.0
                #    Table version: 2.00
                #    Table format:  Format A
                #    Date:          04/24/96
                #    Contact: Shawn.Steele@microsoft.com
                #                   
                #    General notes: none
                #
                #    Format: Three tab-separated columns
                #        Column #1 is the cp866_DOSCyrillicRussian code (in hex)
                #        Column #2 is the Unicode (in hex as 0xXXXX)
                #        Column #3 is the Unicode name (follows a comment sign, '#')
                #
                #    The entries are in cp866_DOSCyrillicRussian order
                #
                 */
                case 0x00: return (char)0x0000;	//NULL
                case 0x01: return (char)0x0001;	//START OF HEADING
                case 0x02: return (char)0x0002;	//START OF TEXT
                case 0x03: return (char)0x0003;	//END OF TEXT
                case 0x04: return (char)0x0004;	//END OF TRANSMISSION
                case 0x05: return (char)0x0005;	//ENQUIRY
                case 0x06: return (char)0x0006;	//ACKNOWLEDGE
                case 0x07: return (char)0x0007;	//BELL
                case 0x08: return (char)0x0008;	//BACKSPACE
                case 0x09: return (char)0x0009;	//HORIZONTAL TABULATION
                case 0x0a: return (char)0x000a;	//LINE FEED
                case 0x0b: return (char)0x000b;	//VERTICAL TABULATION
                case 0x0c: return (char)0x000c;	//FORM FEED
                case 0x0d: return (char)0x000d;	//CARRIAGE RETURN
                case 0x0e: return (char)0x000e;	//SHIFT OUT
                case 0x0f: return (char)0x000f;	//SHIFT IN
                case 0x10: return (char)0x0010;	//DATA LINK ESCAPE
                case 0x11: return (char)0x0011;	//DEVICE CONTROL ONE
                case 0x12: return (char)0x0012;	//DEVICE CONTROL TWO
                case 0x13: return (char)0x0013;	//DEVICE CONTROL THREE
                case 0x14: return (char)0x0014;	//DEVICE CONTROL FOUR
                case 0x15: return (char)0x0015;	//NEGATIVE ACKNOWLEDGE
                case 0x16: return (char)0x0016;	//SYNCHRONOUS IDLE
                case 0x17: return (char)0x0017;	//END OF TRANSMISSION BLOCK
                case 0x18: return (char)0x0018;	//CANCEL
                case 0x19: return (char)0x0019;	//END OF MEDIUM
                case 0x1a: return (char)0x001a;	//SUBSTITUTE
                case 0x1b: return (char)0x001b;	//ESCAPE
                case 0x1c: return (char)0x001c;	//FILE SEPARATOR
                case 0x1d: return (char)0x001d;	//GROUP SEPARATOR
                case 0x1e: return (char)0x001e;	//RECORD SEPARATOR
                case 0x1f: return (char)0x001f;	//UNIT SEPARATOR
                case 0x20: return (char)0x0020;	//SPACE
                case 0x21: return (char)0x0021;	//EXCLAMATION MARK
                case 0x22: return (char)0x0022;	//QUOTATION MARK
                case 0x23: return (char)0x0023;	//NUMBER SIGN
                case 0x24: return (char)0x0024;	//DOLLAR SIGN
                case 0x25: return (char)0x0025;	//PERCENT SIGN
                case 0x26: return (char)0x0026;	//AMPERSAND
                case 0x27: return (char)0x0027;	//APOSTROPHE
                case 0x28: return (char)0x0028;	//LEFT PARENTHESIS
                case 0x29: return (char)0x0029;	//RIGHT PARENTHESIS
                case 0x2a: return (char)0x002a;	//ASTERISK
                case 0x2b: return (char)0x002b;	//PLUS SIGN
                case 0x2c: return (char)0x002c;	//COMMA
                case 0x2d: return (char)0x002d;	//HYPHEN-MINUS
                case 0x2e: return (char)0x002e;	//FULL STOP
                case 0x2f: return (char)0x002f;	//SOLIDUS
                case 0x30: return (char)0x0030;	//DIGIT ZERO
                case 0x31: return (char)0x0031;	//DIGIT ONE
                case 0x32: return (char)0x0032;	//DIGIT TWO
                case 0x33: return (char)0x0033;	//DIGIT THREE
                case 0x34: return (char)0x0034;	//DIGIT FOUR
                case 0x35: return (char)0x0035;	//DIGIT FIVE
                case 0x36: return (char)0x0036;	//DIGIT SIX
                case 0x37: return (char)0x0037;	//DIGIT SEVEN
                case 0x38: return (char)0x0038;	//DIGIT EIGHT
                case 0x39: return (char)0x0039;	//DIGIT NINE
                case 0x3a: return (char)0x003a;	//COLON
                case 0x3b: return (char)0x003b;	//SEMICOLON
                case 0x3c: return (char)0x003c;	//LESS-THAN SIGN
                case 0x3d: return (char)0x003d;	//EQUALS SIGN
                case 0x3e: return (char)0x003e;	//GREATER-THAN SIGN
                case 0x3f: return (char)0x003f;	//QUESTION MARK
                case 0x40: return (char)0x0040;	//COMMERCIAL AT
                case 0x41: return (char)0x0041;	//LATIN CAPITAL LETTER A
                case 0x42: return (char)0x0042;	//LATIN CAPITAL LETTER B
                case 0x43: return (char)0x0043;	//LATIN CAPITAL LETTER C
                case 0x44: return (char)0x0044;	//LATIN CAPITAL LETTER D
                case 0x45: return (char)0x0045;	//LATIN CAPITAL LETTER E
                case 0x46: return (char)0x0046;	//LATIN CAPITAL LETTER F
                case 0x47: return (char)0x0047;	//LATIN CAPITAL LETTER G
                case 0x48: return (char)0x0048;	//LATIN CAPITAL LETTER H
                case 0x49: return (char)0x0049;	//LATIN CAPITAL LETTER I
                case 0x4a: return (char)0x004a;	//LATIN CAPITAL LETTER J
                case 0x4b: return (char)0x004b;	//LATIN CAPITAL LETTER K
                case 0x4c: return (char)0x004c;	//LATIN CAPITAL LETTER L
                case 0x4d: return (char)0x004d;	//LATIN CAPITAL LETTER M
                case 0x4e: return (char)0x004e;	//LATIN CAPITAL LETTER N
                case 0x4f: return (char)0x004f;	//LATIN CAPITAL LETTER O
                case 0x50: return (char)0x0050;	//LATIN CAPITAL LETTER P
                case 0x51: return (char)0x0051;	//LATIN CAPITAL LETTER Q
                case 0x52: return (char)0x0052;	//LATIN CAPITAL LETTER R
                case 0x53: return (char)0x0053;	//LATIN CAPITAL LETTER S
                case 0x54: return (char)0x0054;	//LATIN CAPITAL LETTER T
                case 0x55: return (char)0x0055;	//LATIN CAPITAL LETTER U
                case 0x56: return (char)0x0056;	//LATIN CAPITAL LETTER V
                case 0x57: return (char)0x0057;	//LATIN CAPITAL LETTER W
                case 0x58: return (char)0x0058;	//LATIN CAPITAL LETTER X
                case 0x59: return (char)0x0059;	//LATIN CAPITAL LETTER Y
                case 0x5a: return (char)0x005a;	//LATIN CAPITAL LETTER Z
                case 0x5b: return (char)0x005b;	//LEFT SQUARE BRACKET
                case 0x5c: return (char)0x005c;	//REVERSE SOLIDUS
                case 0x5d: return (char)0x005d;	//RIGHT SQUARE BRACKET
                case 0x5e: return (char)0x005e;	//CIRCUMFLEX ACCENT
                case 0x5f: return (char)0x005f;	//LOW LINE
                case 0x60: return (char)0x0060;	//GRAVE ACCENT
                case 0x61: return (char)0x0061;	//LATIN SMALL LETTER A
                case 0x62: return (char)0x0062;	//LATIN SMALL LETTER B
                case 0x63: return (char)0x0063;	//LATIN SMALL LETTER C
                case 0x64: return (char)0x0064;	//LATIN SMALL LETTER D
                case 0x65: return (char)0x0065;	//LATIN SMALL LETTER E
                case 0x66: return (char)0x0066;	//LATIN SMALL LETTER F
                case 0x67: return (char)0x0067;	//LATIN SMALL LETTER G
                case 0x68: return (char)0x0068;	//LATIN SMALL LETTER H
                case 0x69: return (char)0x0069;	//LATIN SMALL LETTER I
                case 0x6a: return (char)0x006a;	//LATIN SMALL LETTER J
                case 0x6b: return (char)0x006b;	//LATIN SMALL LETTER K
                case 0x6c: return (char)0x006c;	//LATIN SMALL LETTER L
                case 0x6d: return (char)0x006d;	//LATIN SMALL LETTER M
                case 0x6e: return (char)0x006e;	//LATIN SMALL LETTER N
                case 0x6f: return (char)0x006f;	//LATIN SMALL LETTER O
                case 0x70: return (char)0x0070;	//LATIN SMALL LETTER P
                case 0x71: return (char)0x0071;	//LATIN SMALL LETTER Q
                case 0x72: return (char)0x0072; //LATIN SMALL LETTER R
                case 0x73: return (char)0x0073;	//LATIN SMALL LETTER S
                case 0x74: return (char)0x0074;	//LATIN SMALL LETTER T
                case 0x75: return (char)0x0075;	//LATIN SMALL LETTER U
                case 0x76: return (char)0x0076;	//LATIN SMALL LETTER V
                case 0x77: return (char)0x0077;	//LATIN SMALL LETTER W
                case 0x78: return (char)0x0078;	//LATIN SMALL LETTER X
                case 0x79: return (char)0x0079;	//LATIN SMALL LETTER Y
                case 0x7a: return (char)0x007a;	//LATIN SMALL LETTER Z
                case 0x7b: return (char)0x007b;	//LEFT CURLY BRACKET
                case 0x7c: return (char)0x007c;	//VERTICAL LINE
                case 0x7d: return (char)0x007d;	//RIGHT CURLY BRACKET
                case 0x7e: return (char)0x007e;	//TILDE
                case 0x7f: return (char)0x007f;	//DELETE
                case 0x80: return (char)0x0410;	//CYRILLIC CAPITAL LETTER A
                case 0x81: return (char)0x0411;	//CYRILLIC CAPITAL LETTER BE
                case 0x82: return (char)0x0412;	//CYRILLIC CAPITAL LETTER VE
                case 0x83: return (char)0x0413;	//CYRILLIC CAPITAL LETTER GHE
                case 0x84: return (char)0x0414;	//CYRILLIC CAPITAL LETTER DE
                case 0x85: return (char)0x0415;	//CYRILLIC CAPITAL LETTER IE
                case 0x86: return (char)0x0416;	//CYRILLIC CAPITAL LETTER ZHE
                case 0x87: return (char)0x0417;	//CYRILLIC CAPITAL LETTER ZE
                case 0x88: return (char)0x0418;	//CYRILLIC CAPITAL LETTER I
                case 0x89: return (char)0x0419;	//CYRILLIC CAPITAL LETTER SHORT I
                case 0x8a: return (char)0x041a;	//CYRILLIC CAPITAL LETTER KA
                case 0x8b: return (char)0x041b;	//CYRILLIC CAPITAL LETTER EL
                case 0x8c: return (char)0x041c;	//CYRILLIC CAPITAL LETTER EM
                case 0x8d: return (char)0x041d;	//CYRILLIC CAPITAL LETTER EN
                case 0x8e: return (char)0x041e;	//CYRILLIC CAPITAL LETTER O
                case 0x8f: return (char)0x041f;	//CYRILLIC CAPITAL LETTER PE
                case 0x90: return (char)0x0420;	//CYRILLIC CAPITAL LETTER ER
                case 0x91: return (char)0x0421;	//CYRILLIC CAPITAL LETTER ES
                case 0x92: return (char)0x0422;	//CYRILLIC CAPITAL LETTER TE
                case 0x93: return (char)0x0423;	//CYRILLIC CAPITAL LETTER U
                case 0x94: return (char)0x0424;	//CYRILLIC CAPITAL LETTER EF
                case 0x95: return (char)0x0425;	//CYRILLIC CAPITAL LETTER HA
                case 0x96: return (char)0x0426;	//CYRILLIC CAPITAL LETTER TSE
                case 0x97: return (char)0x0427;	//CYRILLIC CAPITAL LETTER CHE
                case 0x98: return (char)0x0428;	//CYRILLIC CAPITAL LETTER SHA
                case 0x99: return (char)0x0429;	//CYRILLIC CAPITAL LETTER SHCHA
                case 0x9a: return (char)0x042a;	//CYRILLIC CAPITAL LETTER HARD SIGN
                case 0x9b: return (char)0x042b;	//CYRILLIC CAPITAL LETTER YERU
                case 0x9c: return (char)0x042c;	//CYRILLIC CAPITAL LETTER SOFT SIGN
                case 0x9d: return (char)0x042d;	//CYRILLIC CAPITAL LETTER E
                case 0x9e: return (char)0x042e;	//CYRILLIC CAPITAL LETTER YU
                case 0x9f: return (char)0x042f;	//CYRILLIC CAPITAL LETTER YA
                case 0xa0: return (char)0x0430;	//CYRILLIC SMALL LETTER A
                case 0xa1: return (char)0x0431;	//CYRILLIC SMALL LETTER BE
                case 0xa2: return (char)0x0432;	//CYRILLIC SMALL LETTER VE
                case 0xa3: return (char)0x0433;	//CYRILLIC SMALL LETTER GHE
                case 0xa4: return (char)0x0434;	//CYRILLIC SMALL LETTER DE
                case 0xa5: return (char)0x0435;	//CYRILLIC SMALL LETTER IE
                case 0xa6: return (char)0x0436;	//CYRILLIC SMALL LETTER ZHE
                case 0xa7: return (char)0x0437;	//CYRILLIC SMALL LETTER ZE
                case 0xa8: return (char)0x0438;	//CYRILLIC SMALL LETTER I
                case 0xa9: return (char)0x0439;	//CYRILLIC SMALL LETTER SHORT I
                case 0xaa: return (char)0x043a;	//CYRILLIC SMALL LETTER KA
                case 0xab: return (char)0x043b;	//CYRILLIC SMALL LETTER EL
                case 0xac: return (char)0x043c;	//CYRILLIC SMALL LETTER EM
                case 0xad: return (char)0x043d;	//CYRILLIC SMALL LETTER EN
                case 0xae: return (char)0x043e;	//CYRILLIC SMALL LETTER O
                case 0xaf: return (char)0x043f;	//CYRILLIC SMALL LETTER PE
                case 0xb0: return (char)0x2591;	//LIGHT SHADE
                case 0xb1: return (char)0x2592;	//MEDIUM SHADE
                case 0xb2: return (char)0x2593;	//DARK SHADE
                case 0xb3: return (char)0x2502;	//BOX DRAWINGS LIGHT VERTICAL
                case 0xb4: return (char)0x2524;	//BOX DRAWINGS LIGHT VERTICAL AND LEFT
                case 0xb5: return (char)0x2561;	//BOX DRAWINGS VERTICAL SINGLE AND LEFT DOUBLE
                case 0xb6: return (char)0x2562;	//BOX DRAWINGS VERTICAL DOUBLE AND LEFT SINGLE
                case 0xb7: return (char)0x2556;	//BOX DRAWINGS DOWN DOUBLE AND LEFT SINGLE
                case 0xb8: return (char)0x2555;	//BOX DRAWINGS DOWN SINGLE AND LEFT DOUBLE
                case 0xb9: return (char)0x2563;	//BOX DRAWINGS DOUBLE VERTICAL AND LEFT
                case 0xba: return (char)0x2551;	//BOX DRAWINGS DOUBLE VERTICAL
                case 0xbb: return (char)0x2557;	//BOX DRAWINGS DOUBLE DOWN AND LEFT
                case 0xbc: return (char)0x255d;	//BOX DRAWINGS DOUBLE UP AND LEFT
                case 0xbd: return (char)0x255c;	//BOX DRAWINGS UP DOUBLE AND LEFT SINGLE
                case 0xbe: return (char)0x255b;	//BOX DRAWINGS UP SINGLE AND LEFT DOUBLE
                case 0xbf: return (char)0x2510;	//BOX DRAWINGS LIGHT DOWN AND LEFT
                case 0xc0: return (char)0x2514;	//BOX DRAWINGS LIGHT UP AND RIGHT
                case 0xc1: return (char)0x2534;	//BOX DRAWINGS LIGHT UP AND HORIZONTAL
                case 0xc2: return (char)0x252c;	//BOX DRAWINGS LIGHT DOWN AND HORIZONTAL
                case 0xc3: return (char)0x251c;	//BOX DRAWINGS LIGHT VERTICAL AND RIGHT
                case 0xc4: return (char)0x2500;	//BOX DRAWINGS LIGHT HORIZONTAL
                case 0xc5: return (char)0x253c;	//BOX DRAWINGS LIGHT VERTICAL AND HORIZONTAL
                case 0xc6: return (char)0x255e;	//BOX DRAWINGS VERTICAL SINGLE AND RIGHT DOUBLE
                case 0xc7: return (char)0x255f;	//BOX DRAWINGS VERTICAL DOUBLE AND RIGHT SINGLE
                case 0xc8: return (char)0x255a;	//BOX DRAWINGS DOUBLE UP AND RIGHT
                case 0xc9: return (char)0x2554;	//BOX DRAWINGS DOUBLE DOWN AND RIGHT
                case 0xca: return (char)0x2569;	//BOX DRAWINGS DOUBLE UP AND HORIZONTAL
                case 0xcb: return (char)0x2566;	//BOX DRAWINGS DOUBLE DOWN AND HORIZONTAL
                case 0xcc: return (char)0x2560;	//BOX DRAWINGS DOUBLE VERTICAL AND RIGHT
                case 0xcd: return (char)0x2550;	//BOX DRAWINGS DOUBLE HORIZONTAL
                case 0xce: return (char)0x256c;	//BOX DRAWINGS DOUBLE VERTICAL AND HORIZONTAL
                case 0xcf: return (char)0x2567;	//BOX DRAWINGS UP SINGLE AND HORIZONTAL DOUBLE
                case 0xd0: return (char)0x2568;	//BOX DRAWINGS UP DOUBLE AND HORIZONTAL SINGLE
                case 0xd1: return (char)0x2564;	//BOX DRAWINGS DOWN SINGLE AND HORIZONTAL DOUBLE
                case 0xd2: return (char)0x2565;	//BOX DRAWINGS DOWN DOUBLE AND HORIZONTAL SINGLE
                case 0xd3: return (char)0x2559; //BOX DRAWINGS UP DOUBLE AND RIGHT SINGLE
                case 0xd4: return (char)0x2558;	//BOX DRAWINGS UP SINGLE AND RIGHT DOUBLE
                case 0xd5: return (char)0x2552;	//BOX DRAWINGS DOWN SINGLE AND RIGHT DOUBLE
                case 0xd6: return (char)0x2553;	//BOX DRAWINGS DOWN DOUBLE AND RIGHT SINGLE
                case 0xd7: return (char)0x256b;	//BOX DRAWINGS VERTICAL DOUBLE AND HORIZONTAL SINGLE
                case 0xd8: return (char)0x256a;	//BOX DRAWINGS VERTICAL SINGLE AND HORIZONTAL DOUBLE
                case 0xd9: return (char)0x2518;	//BOX DRAWINGS LIGHT UP AND LEFT
                case 0xda: return (char)0x250c;	//BOX DRAWINGS LIGHT DOWN AND RIGHT
                case 0xdb: return (char)0x2588;	//FULL BLOCK
                case 0xdc: return (char)0x2584;	//LOWER HALF BLOCK
                case 0xdd: return (char)0x258c;	//LEFT HALF BLOCK
                case 0xde: return (char)0x2590;	//RIGHT HALF BLOCK
                case 0xdf: return (char)0x2580;	//UPPER HALF BLOCK
                case 0xe0: return (char)0x0440;	//CYRILLIC SMALL LETTER ER
                case 0xe1: return (char)0x0441;	//CYRILLIC SMALL LETTER ES
                case 0xe2: return (char)0x0442;	//CYRILLIC SMALL LETTER TE
                case 0xe3: return (char)0x0443;	//CYRILLIC SMALL LETTER U
                case 0xe4: return (char)0x0444;	//CYRILLIC SMALL LETTER EF
                case 0xe5: return (char)0x0445;	//CYRILLIC SMALL LETTER HA
                case 0xe6: return (char)0x0446;	//CYRILLIC SMALL LETTER TSE
                case 0xe7: return (char)0x0447;	//CYRILLIC SMALL LETTER CHE
                case 0xe8: return (char)0x0448;	//CYRILLIC SMALL LETTER SHA
                case 0xe9: return (char)0x0449;	//CYRILLIC SMALL LETTER SHCHA
                case 0xea: return (char)0x044a;	//CYRILLIC SMALL LETTER HARD SIGN
                case 0xeb: return (char)0x044b;	//CYRILLIC SMALL LETTER YERU
                case 0xec: return (char)0x044c;	//CYRILLIC SMALL LETTER SOFT SIGN
                case 0xed: return (char)0x044d;	//CYRILLIC SMALL LETTER E
                case 0xee: return (char)0x044e;	//CYRILLIC SMALL LETTER YU
                case 0xef: return (char)0x044f;	//CYRILLIC SMALL LETTER YA
                case 0xf0: return (char)0x0401;	//CYRILLIC CAPITAL LETTER IO
                case 0xf1: return (char)0x0451;	//CYRILLIC SMALL LETTER IO
                case 0xf2: return (char)0x0404;	//CYRILLIC CAPITAL LETTER UKRAINIAN IE
                case 0xf3: return (char)0x0454;	//CYRILLIC SMALL LETTER UKRAINIAN IE
                case 0xf4: return (char)0x0407;	//CYRILLIC CAPITAL LETTER YI
                case 0xf5: return (char)0x0457;	//CYRILLIC SMALL LETTER YI
                case 0xf6: return (char)0x040e;	//CYRILLIC CAPITAL LETTER SHORT U
                case 0xf7: return (char)0x045e;	//CYRILLIC SMALL LETTER SHORT U
                case 0xf8: return (char)0x00b0;	//DEGREE SIGN
                case 0xf9: return (char)0x2219;	//BULLET OPERATOR
                case 0xfa: return (char)0x00b7;	//MIDDLE DOT
                case 0xfb: return (char)0x221a;	//SQUARE ROOT
                case 0xfc: return (char)0x2116;	//NUMERO SIGN
                case 0xfd: return (char)0x00a4;	//CURRENCY SIGN
                case 0xfe: return (char)0x25a0;	//BLACK SQUARE
                case 0xff: return (char)0x00a0;	//NO-BREAK SPACE
            }
            return (char)0x0000;
        }
    }

    public class AnswerData
    {
        public byte Dad { get; set; }       // байт адреса приёмника
        public byte Sad { get; set; }       // байт адреса источника
        public byte Fnc { get; set; }       // код функции при ответе
        public int Channel { get; set; }
        public int Parameter { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Time { get; set; }

        public override string ToString()
        {
            return $"{Dad} {Sad} {Fnc} {Channel} {Parameter} {Value} {Unit} {Time}";
        }
    }
}
