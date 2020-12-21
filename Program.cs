using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading;
using System.Xml.Linq;

namespace L2M
{
    class Program
    {
        static void Main(string[] args)
        {
            var configName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "L2M.xml");
            // чтение конфигурационного файла
            var xdoc = XDocument.Load(configName);
            XElement listenTcp = xdoc.Element("Config").Element("ListenTcp");
            ReadConfigParameters(listenTcp, out IPAddress ipAddress, out int ipPort, out int sendTimeout, out int receiveTimeout);
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
                ReadConfigParameters(fetchingTcp, out ipAddress, out ipPort, out sendTimeout, out receiveTimeout);
                var parameters = FillParameters(fetchingTcp);
                var fetcher = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
                fetcher.DoWork += LogikaFetcher_DoWork;
                fetcher.ProgressChanged += LogikaFetcher_ProgressChanged;
                tcptuning = new TcpTuning
                {
                    Address = ipAddress,
                    Port = ipPort,
                    SendTimeout = sendTimeout,
                    ReceiveTimeout = receiveTimeout,
                    Parameters = parameters,
                    //FetchArchives = fetchArchives
                };
                fetcher.RunWorkerAsync(tcptuning);
            }
            // Если запускает пользователь сам
            if (Environment.UserInteractive)
            {
                Console.WriteLine("MODBUS listening service loaded.");
                Console.WriteLine("Ver. 0.3\n");
                Console.WriteLine("Press any key for exit...");
                Console.ReadKey();
            }
            else
            {
                var servicesToRun = new ServiceBase[] { new WinService() };
                ServiceBase.Run(servicesToRun);
            }
        }

        /// <summary>
        /// Чтение конфигурационных параметров
        /// </summary>
        /// <param name="parentElement">Родительский элемент</param>
        /// <param name="ipAddress">IP Address</param>
        /// <param name="ipPort">IP Port</param>
        /// <param name="sendTimeout">Send timeout</param>
        /// <param name="receiveTimeout">Receive timeout</param>
        private static void ReadConfigParameters(XElement parentElement, out IPAddress ipAddress, out int ipPort, out int sendTimeout, out int receiveTimeout)
        {
            XElement element = parentElement.Element("IpAddress");
            ipAddress = null;
            if (element == null || !IPAddress.TryParse(element.Value, out ipAddress))
                ipAddress = IPAddress.Parse("127.0.0.1");
            element = parentElement.Element("IpPort");
            if (element == null || !int.TryParse(element.Value, out ipPort))
                ipPort = 502;
            element = parentElement.Element("SendTimeout");
            if (element == null || !int.TryParse(element.Value, out sendTimeout))
                sendTimeout = 5000;
            element = parentElement.Element("ReceiveTimeout");
            if (element == null || !int.TryParse(element.Value, out receiveTimeout))
                receiveTimeout = 5000;
        }

        /// <summary>
        /// Чтение параметров опроса
        /// </summary>
        /// <param name="parentElement">Родительский элемент</param>
        /// <returns></returns>
        private static IEnumerable<RequestData> FillParameters(XElement parentElement)
        {
            var parameters = new List<RequestData>();
            byte dad = 0, sad = 0, nodeAddr = 0;
            int channel = 0, parameter = 0, answerWait = 0, arrayIndexNumber = 0;
            ModbusTable modbusTable = ModbusTable.None;
            LogikaParam paramKind = LogikaParam.Parameter;
            ushort startAddr = 0;
            string dataFormat = "";
            bool good = true;
            foreach (var item in parentElement.Element("Runtime").Elements("LogikaItem"))
            {
                var element = item.Element("Dad");
                if (element == null || !byte.TryParse(element.Value, out dad))
                    good = false;
                element = item.Element("Sad");
                if (element == null || !byte.TryParse(element.Value, out sad))
                    good = false;
                element = item.Element("Channel");
                if (element == null || !int.TryParse(element.Value, out channel))
                    good = false;
                element = item.Element("Parameter");
                if (element != null)
                {
                    paramKind = LogikaParam.Parameter;
                    if (!int.TryParse(element.Value, out parameter))
                        good = false;
                }
                else
                {
                    element = item.Element("IndexArray");
                    if (element != null)
                    {
                        paramKind = LogikaParam.IndexArray;
                        if (!int.TryParse(element.Value, out parameter))
                            good = false;
                        element = item.Element("ArrayIndexNumber");
                        if (element == null || !int.TryParse(element.Value, out arrayIndexNumber))
                            good = false;

                    }
                    else
                        good = false;
                }

                element = item.Element("ModbusNode");
                if (element == null || !byte.TryParse(element.Value, out nodeAddr))
                    good = false;
                element = item.Element("DataFormat");
                if (element != null)
                    dataFormat = element.Value;
                else
                    good = false;
                element = item.Element("AnswerWait");
                if (element == null || !int.TryParse(element.Value, out answerWait))
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
                        ParameterKind = paramKind,
                        Parameter = parameter,
                        ArrayIndexNumber = arrayIndexNumber,
                        NodeAddr = nodeAddr,
                        StartAddr = startAddr,
                        ModbusTable = modbusTable,
                        FormatData = dataFormat,
                        AnswerWait = answerWait
                    });
                }
            }
            return parameters;
        }

        /// <summary>
        /// Работа потока для опроса ЛОГИКИ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void LogikaFetcher_DoWork(object sender, DoWorkEventArgs e)
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
                            var paramsToWriteExists = Modbus.ParamsToWriteExists();
                            foreach (var p in parameters.Parameters)
                            {
                                switch (p.ParameterKind)
                                {
                                    case LogikaParam.Parameter:
                                        if (paramsToWriteExists && p.ModbusTable == ModbusTable.Holdings)
                                        {
                                            var value = Modbus.GetParamValue(new ParamAddr(p.NodeAddr,
                                                Modbus.ModifyToModbusRegisterAddress(p.StartAddr, ModbusTable.Holdings)));
                                            if (!string.IsNullOrWhiteSpace(value))
                                            {
                                                Logika.WriteToParameter(socket, p.Dad, p.Sad, p.Channel, p.Parameter, p.AnswerWait, value);
                                            }
                                        }
                                        Logika.FetchParameter(socket, p.Dad, p.Sad, p.Channel, p.Parameter,
                                                              p.NodeAddr, p.ModbusTable, p.StartAddr, p.FormatData, p.AnswerWait);
                                        break;
                                    case LogikaParam.IndexArray:
                                        {
                                            if (paramsToWriteExists && p.ModbusTable == ModbusTable.Holdings)
                                            {
                                                var value = Modbus.GetParamValue(new ParamAddr(p.NodeAddr, 
                                                    Modbus.ModifyToModbusRegisterAddress(p.StartAddr, ModbusTable.Holdings)));
                                                if (!string.IsNullOrWhiteSpace(value))
                                                {
                                                    //Logika.WriteToIndexArray(socket, dad: 1, sad: 3, channel: 1, arrayNumber: 125, arrayIndex: 5, answerWait: 150, value: "1.939");
                                                    Logika.WriteToIndexArray(socket, p.Dad, p.Sad, p.Channel, p.Parameter, p.ArrayIndexNumber, p.AnswerWait, value);
                                                }
                                            }
                                            Logika.FetchIndexArray(socket, p.Dad, p.Sad, p.Channel, p.Parameter, p.ArrayIndexNumber,
                                                                  p.NodeAddr, p.ModbusTable, p.StartAddr, p.FormatData, p.AnswerWait);
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    worker.ReportProgress(0, ex.Message);
                }
            }
        }

        private static void LogikaFetcher_ProgressChanged(object sender, ProgressChangedEventArgs e)
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
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(header1)));
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(header2)));
                                        bytesCount = Convert.ToByte(regCount * 2);
                                        packetLen = Convert.ToUInt16(bytesCount + 3);
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(packetLen)));
                                        answer.Add(nodeAddr);
                                        answer.Add(funcCode);
                                        answer.Add(bytesCount);
                                        //

                                        for (ushort i = 0; i < regCount; i++)
                                        {
                                            var regAddr = Modbus.ModifyToModbusRegisterAddress((ushort)(i + startAddr), (ModbusTable)funcCode);
                                            ushort value = Modbus.GetRegisterValue(nodeAddr, regAddr);
                                            answer.AddRange(BitConverter.GetBytes(value));
                                        }

                                        msg = answer.ToArray();
                                        stream.Write(msg, 0, msg.Length);
                                        break;
                                    //case 6: // write one register
                                    //    Modbus.SetRegisterValue(nodeAddr, Modbus.ModifyToModbusRegisterAddress(startAddr, ModbusTable.Holdings), singleValue);
                                    //    //-------------------
                                    //    answer = new List<byte>();
                                    //    answer.AddRange(BitConverter.GetBytes(Modbus.Swap(header1)));
                                    //    answer.AddRange(BitConverter.GetBytes(Modbus.Swap(header2)));
                                    //    answer.AddRange(BitConverter.GetBytes(Modbus.Swap(6)));
                                    //    answer.Add(nodeAddr);
                                    //    answer.Add(funcCode);
                                    //    answer.AddRange(BitConverter.GetBytes(Modbus.Swap(startAddr)));
                                    //    answer.AddRange(BitConverter.GetBytes(Modbus.Swap(regCount)));
                                    //    msg = answer.ToArray();
                                    //    stream.Write(msg, 0, msg.Length);
                                    //    break;
                                    case 16: // write several registers
                                        var n = 13;
                                        ushort addr = startAddr;
                                        for (ushort i = 0; i < regCount; i++)
                                        {
                                            var regAddr = Modbus.ModifyToModbusRegisterAddress(addr, ModbusTable.Holdings);
                                            ushort value = BitConverter.ToUInt16(bytes, n);
                                            Modbus.SetRegisterValue(nodeAddr, regAddr, value, true);
                                            n += 2;  // коррекция позиции смещения в принятых данных для записи
                                            addr += 1;
                                        }
                                        if (regCount == 2)
                                        {
                                            var regAddr = Modbus.ModifyToModbusRegisterAddress(startAddr, ModbusTable.Holdings);
                                            var value = (float)Modbus.TypedValueFromRegistersArray(nodeAddr, regAddr, typeof(float), true);
                                            Modbus.SetParamValue(new ParamAddr(nodeAddr, regAddr), string.Format(CultureInfo.GetCultureInfo("en-US"), "{0}", value));
                                        }
                                        //-------------------
                                        answer = new List<byte>();
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(header1)));
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(header2)));
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(6)));
                                        answer.Add(nodeAddr);
                                        answer.Add(funcCode);
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(startAddr)));
                                        answer.AddRange(BitConverter.GetBytes(Modbus.Swap(regCount)));
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

        /// <summary>
        /// Отображаем значения регистров в консоли программы
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ModbusListener_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Console.WriteLine($"{e.UserState}");
        }
    }
}
