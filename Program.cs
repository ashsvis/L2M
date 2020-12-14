using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace L2M
{
    class Program
    {
        private static readonly ushort[] registers = new ushort[50000];

        private static readonly object locker = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("MODBUS listening service loaded.");
            Console.WriteLine("Ver. 0.1\n");
            // запуск потока для обработки устройства по протоколу Modbus Tcp
            var worker = new BackgroundWorker { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            worker.DoWork += ModbusWorker_DoWork;
            worker.RunWorkerCompleted += ModbusWorker_RunWorkerCompleted;
            worker.ProgressChanged += ModbusWorker_ProgressChanged;
            var tcptuning = new TcpTuning
            {
                //Address = ipAddr,
                //Port = ipPort,
                //SendTimeout = sendTimeout,
                //ReceiveTimeout = receiveTimeout,
                //FetchParams = fetchParams,
                //FetchArchives = fetchArchives
            };
            worker.RunWorkerAsync(tcptuning);

            Console.WriteLine("Press any key for exit...");
            Console.ReadKey();
            worker.CancelAsync();
        }

        private static void ModbusWorker_DoWork(object sender, DoWorkEventArgs e)
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
                                //Say("Q:" + string.Join(",", list));
                                worker.ReportProgress(0, "Q:" + string.Join(",", list));

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
                                        worker.ReportProgress(0, $"node:{nodeAddr} func:{funcCode} addr:{startAddr} count:{regCount}");

                                        for (ushort i = 0; i < regCount; i++)
                                        {
                                            var regAddr = ModifyToModbusRegisterAddress(i, funcCode);
                                            ushort value = GetRegisterValue(regAddr);
                                            answer.AddRange(BitConverter.GetBytes(value));
                                        }

                                        msg = answer.ToArray();
                                        stream.Write(msg, 0, msg.Length);
                                        break;
                                    case 6: // write one register
                                        SetRegisterValue(ModifyToModbusRegisterAddress(startAddr, 3), Swap(singleValue));
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
                                            var regAddr = ModifyToModbusRegisterAddress(addr, 3);
                                            ushort value = BitConverter.ToUInt16(bytes, n);
                                            SetRegisterValue(regAddr, value);
                                            n = n + 2;
                                            addr += 2;
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
                                //TranslateChannelMessage(parameters.ChannelId, ex.Message);
                                worker.ReportProgress(0, ex.Message);
                        }
                    });
                }
                catch (SocketException ex)
                {
                    if (!worker.CancellationPending)
                        //TranslateChannelMessage(parameters.ChannelId, $"Ошибка приёма: {ex.Message}");
                        worker.ReportProgress(0, ex.Message);
                    break;
                }
            } while (!worker.CancellationPending);
            listener.Stop();

        }

        private static ushort GetRegisterValue(ushort index)
        {
            lock (locker)
            {
                return registers[index - 1];
            }
        }

        private static void SetRegisterValue(ushort index, ushort value)
        {
            lock (locker)
            {
                registers[index - 1] = value;
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

        private static ushort ModifyToModbusRegisterAddress(ushort startAddr, byte funcCode)
        {
            switch (funcCode)
            {
                case 1:
                    return Convert.ToUInt16(1 + startAddr);       // coils
                case 2:
                    return Convert.ToUInt16(10001 + startAddr);   // contacts
                case 3:
                    return Convert.ToUInt16(40001 + startAddr);   // holdings
                case 4:
                    return Convert.ToUInt16(30001 + startAddr);   // inputs
            }
            throw new NotImplementedException();
        }

        private static void ModbusWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
                Console.WriteLine($"{e.UserState}");
        }

        private static void ModbusWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //throw new NotImplementedException();
        }
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
        //public IEnumerable<RequestData> FetchRequests { get; set; } = new List<RequestData>();
    }
}
