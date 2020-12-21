using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Timers;

namespace DataEventClient
{
    public enum ClientConnectionStatus
    {
        Closed,
        Opening,
        Opened,
        Faulted
    }

    public class EventClient
    {
        private CallbackHandler _callback;
        private string[] _categories;
        private PropertyUpdateWrapper _propertyUpdate;
        private ClientErrorWrapper _showError;
        private System.Timers.Timer _faultTimer;
        private ConnectionStatusWrapper _connectionStatus;

        public Guid ClientId { get; } = Guid.NewGuid();

        public void Connect(string[] categories, PropertyUpdateWrapper propertyUpdate, ClientErrorWrapper showError, ConnectionStatusWrapper connectionStatus)
        {
            _categories = categories;
            _propertyUpdate = propertyUpdate;
            _showError = showError;
            _connectionStatus = connectionStatus;
            ThreadPool.QueueUserWorkItem(param =>
            {
                _callback = new CallbackHandler(ClientId, ConnectionStatus);
                Thread.Sleep(100);
                _callback.RegisterForUpdates(categories, propertyUpdate, showError);
            });
            _faultTimer = new System.Timers.Timer(15 * 1000) { AutoReset = false };
            _faultTimer.Elapsed += Reconnecting;
        }

        private void Reconnecting(object sender, ElapsedEventArgs e)
        {
            if (_showError != null)
            {
                var mess = "Попытка подключиться к серверу событий после сбоя связи...";
                _showError(mess);
            }
            Reconnect();
        }

        private void ConnectionStatus(Guid clientId, ClientConnectionStatus status)
        {
            if (status == ClientConnectionStatus.Faulted)
            {
                if (_showError != null)
                {
                    var mess = "Канал связи перешёл в состояние \"Ошибка\"";
                    _showError(mess);
                }
                _faultTimer.Enabled = true;
            }
            _connectionStatus?.Invoke(clientId, status);
        }

        public void UpdateProperty(string category, string pointname, string propname, string value,
                                          bool nocash = false)
        {
            if (_callback != null)
                ThreadPool.QueueUserWorkItem(param =>
                    _callback.UpdateProperty(category, pointname, propname, value, nocash));
        }

        public void SubscribeValues()
        {
            if (_callback != null) _callback.SubscribeValues();
        }

        public void Disconnect()
        {
            if (_callback != null) new Thread(() => _callback.Disconnect()).Start();
        }

        public void Reconnect()
        {
            ThreadPool.QueueUserWorkItem(param =>
            {
                Disconnect();
                Thread.Sleep(500);
                Connect(_categories, _propertyUpdate, _showError, _connectionStatus);
            });
        }

        //public void SendCommand(string address, int command, ushort[] hregs)
        //{
        //    if (_callback != null)
        //    {
        //        _callback.SendCommand(address, command, hregs);
        //    }
        //}
    }

    public delegate void PropertyUpdateWrapper(
        DateTime servertime, string category, string pointname, string propname, string value);

    public delegate void ClientErrorWrapper(string errormessage);

    public delegate void ConnectionStatusWrapper(Guid clientId, ClientConnectionStatus status);

    public delegate void ClientFileReceivedWrapper(string tarfilename, int percent, bool complete);

    public class CallbackHandler : DataEventClient.ClientServiceReference.IClientEventServiceCallback, IDisposable
    {
        private readonly Guid _clientId;
        private readonly TimeSpan _timeout = new TimeSpan(0, 1, 30);
        private readonly InstanceContext _site;
        private readonly Binding _binding;
        private readonly ConnectionStatusWrapper _connectionStatus;
        private readonly DataEventClient.ClientServiceReference.ClientEventServiceClient _proxy;

        public CallbackHandler(Guid clientId, ConnectionStatusWrapper connectionStatus)
        {
            _clientId = clientId;
            _connectionStatus = connectionStatus;
            var uri = "net.pipe://localhost/ModbusIntegrationServer";
            _site = new InstanceContext(this);
            _binding = new NetNamedPipeBinding
            {
                OpenTimeout = _timeout,
                SendTimeout = _timeout,
                ReceiveTimeout = _timeout,
                CloseTimeout = _timeout
            };
            _proxy = new DataEventClient.ClientServiceReference.ClientEventServiceClient(_site, _binding, new EndpointAddress(uri));

            _proxy.InnerDuplexChannel.Opened += (sender, args) =>
            {
                _connectionStatus?.Invoke(_clientId, ClientConnectionStatus.Opened);
                ThreadPool.QueueUserWorkItem(arg =>
                {
                    Thread.Sleep(1000);
                    SubscribeValues();
                });
            };
            _proxy.InnerDuplexChannel.Opening += (sender, args) =>
            {
                _connectionStatus?.Invoke(_clientId, ClientConnectionStatus.Opening);
            };
            _proxy.InnerDuplexChannel.Closed += (sender, args) =>
            {
                _connectionStatus?.Invoke(_clientId, ClientConnectionStatus.Closed);
            };

            _proxy.InnerDuplexChannel.Faulted += (sender, args) =>
            {
                _connectionStatus?.Invoke(_clientId, ClientConnectionStatus.Faulted);
            };
        }

        private PropertyUpdateWrapper _propertyUpdate;
        private ClientErrorWrapper _showError;

        /// <summary>Регистрирует клиента на подписку</summary>
        /// <param name="categories">строковый массив категорий для подписки</param>
        /// <param name="propertyUpdate">делегат события при изменении значения свойства</param>
        /// <param name="showError">делегат события при ошибке</param>
        /// <param name="fileReceived">делегат события при получении файла</param>
        public bool RegisterForUpdates(string[] categories, PropertyUpdateWrapper propertyUpdate,
                                       ClientErrorWrapper showError = null)
        {
            _propertyUpdate = propertyUpdate;
            _showError = showError;
            try
            {
                _proxy.RegisterForUpdates(_clientId, categories);
                return true;
            }
            catch (EndpointNotFoundException ex)
            {
                SendToErrorsLog("Ошибка подключения к серверу событий: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в RegisterForUpdates(): ", ex.Message);
                SendMessage(message);
                SendToErrorsLog("Ошибка в RegisterForUpdates(: " + ex.FullMessage());
                return false;
            }
        }

        private void SendToErrorsLog(string mess)
        {

        }

        /// <summary>Рассылка всех значений из накопленного кэша сервера вновь подключившемуся клиенту</summary>
        public void SubscribeValues()
        {
            try
            {
                if (_proxy.State.Equals(CommunicationState.Opened))
                    _proxy.SubscribeValues(_clientId);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в SubscribeValues(): ", ex.Message);
                SendMessage(message);
                SendToErrorsLog("Ошибка в SubscribeValues(): " + ex.FullMessage());
            }
        }

        /// <summary>Изменение значения свойства клиентом</summary>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        public void UpdateProperty(string category, string pointname, string propname, string value, bool nocash)
        {
            try
            {
                if (_proxy.State == CommunicationState.Opened)
                    _proxy.UpdateProperty(_clientId, category, pointname, propname, value, nocash);
            }
            catch (CommunicationObjectFaultedException ex)
            {
                SendToErrorsLog("Ошибка CommunicationObjectFaulted в UpdateProperty(): " + ex.Message);
            }
            catch (CommunicationException ex)
            {
                SendToErrorsLog("Ошибка Communication в UpdateProperty(): " + ex.Message);
            }
            catch (Exception ex)
            {
                var message = String.Concat("Ошибка в UpdateProperty(): ", ex.Message);
                SendMessage(message);
                SendToErrorsLog("Ошибка в UpdateProperty(): " + ex.FullMessage());
            }
        }

        private void SendMessage(string message)
        {
            if (_showError == null) return;
            try
            {
                _showError(message);
            }
            catch (Exception ex)
            {
                SendToErrorsLog("Ошибка при выводе сообщения: " + ex.FullMessage());
            }
        }

        public void PropertyUpdated(DateTime servertime, string category, string pointname, string propname,
                                    string value)
        {
            if (_propertyUpdate == null) return;
            try
            {
                _propertyUpdate(servertime, category, pointname, propname, value);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в PropertyUpdated(): ", ex.Message);
                SendMessage(message);
                SendToErrorsLog("Ошибка в PropertyUpdated(): " + ex.FullMessage());
            }
        }

        //public void SendCommand(string address, int command, ushort[] hregs)
        //{
        //    try
        //    {
        //        if (_proxy.State == CommunicationState.Opened)
        //            _proxy.SendCommand(_clientId, address, command, hregs);
        //    }
        //    catch (CommunicationObjectFaultedException ex)
        //    {
        //        SendToErrorsLog("Ошибка в SendCommand(): " + ex.Message);
        //    }
        //    catch (CommunicationException ex)
        //    {
        //        SendToErrorsLog("Ошибка в SendCommand(): " + ex.Message);
        //    }
        //    catch (Exception ex)
        //    {
        //        var message = String.Concat("Ошибка в SendCommand(): ", ex.Message);
        //        SendMessage(message);
        //        SendToErrorsLog("Ошибка в SendCommand(): " + ex.FullMessage());
        //    }

        //}

        public void Disconnect()
        {
            try
            {
                if (!_proxy.State.Equals(CommunicationState.Opened)) return;
                _proxy.Disconnect(_clientId);
                _proxy.InnerDuplexChannel.Close();
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в Disconnect(): ", ex.Message);
                SendMessage(message);
                SendToErrorsLog("Ошибка в Disconnect(): " + ex.FullMessage());
            }
        }

        public void Dispose()
        {

        }
    }
}
