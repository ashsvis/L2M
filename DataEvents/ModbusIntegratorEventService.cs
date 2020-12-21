using System;
using System.Linq;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;

namespace L2M
{
    public class ModbusIntegratorEventService : IClientEventService
    {
        private static readonly ConcurrentDictionary<string, DataItem> Cashprops =
            new ConcurrentDictionary<string, DataItem>();

        /// <summary>Рассылка всех значений из накопленного кэша сервера вновь подключившемуся клиенту</summary>
        /// <param name="clientId">ID клиента</param>
        public void SubscribeValues(Guid clientId)
        {
            ThreadPool.QueueUserWorkItem(param =>
            {
                foreach (var key in Cashprops.Keys)
                {
                    if (!Cashprops.TryGetValue(key, out DataItem item)) continue;
                    CustomUpdateProperty(clientId, item.SnapTime, item.Category, item.Name, item.Prop, item.Value,
                                         true, true);
                }
            });
        }

        /// <summary>Запись значения свойства в словарь</summary>
        /// <param name="category">имя категории</param>
        /// <param name="name">имя объекта</param>
        /// <param name="prop">имя свойства</param>
        /// <param name="value">значение</param>
        internal static bool SetPropValue(string category, string name, string prop, string value)
        {
            var key = GetCashpropsKey(category, name, prop);
            // одинаковые значения игнорируем
            if (Cashprops.TryGetValue(key, out DataItem item) && item.Value.Equals(value)) return false;
            // создаем новый объект хранения
            item = new DataItem
            {
                SnapTime = DateTime.Now,
                Category = category,
                Name = name,
                Prop = prop,
                Value = value
            };
            // добавляем или обновляем значение свойства
            Cashprops.AddOrUpdate(key, item,
                                  (akey, existingVal) =>
                                  {
                                      existingVal.Value = value;
                                      existingVal.SnapTime = DateTime.Now;
                                      return existingVal;
                                  });
            return true;
        }

        private static string GetCashpropsKey(string category, string name, string prop)
        {
            return string.Concat(category.Replace('\t', '_'), "\t", name.Replace('\t', '_'), "\t", prop.Replace('\t', '_'));
        }

        private class DataItem
        {
            public string Category { get; set; }
            public string Name { get; set; }
            public string Prop { get; set; }
            public string Value { get; set; }
            public DateTime SnapTime { get; set; }

            public DataItem()
            {
                Category = string.Empty;
                Name = string.Empty;
                Prop = string.Empty;
                Value = string.Empty;
                SnapTime = DateTime.MinValue;
            }
        }

        private class CallbackInfo
        {
            public IClientCallback ClientCallback;
            public Guid ClientId;
        }

        private class Worker
        {
            public readonly List<CallbackInfo> Callbacks = new List<CallbackInfo>();
            public string Category { get; set; }
        }

        private static readonly Hashtable Workers = new Hashtable();

        /// <summary>Регистрирует клиента на подписку</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="categories">строковый массив категорий для подписки</param>
        public void RegisterForUpdates(Guid clientId, string[] categories)
        {
            new Thread(current =>
            {
                lock (Workers.SyncRoot)
                {
                    var list = new List<string>(categories) { "filetransfer" };
                    foreach (var category in list)
                    {
                        // при необходимости создаем новый рабочий объект, добавляем его
                        // добавляем его в хэш-таблицу и запускаем в отдельном потоке
                        Worker worker;
                        if (!Workers.ContainsKey(category))
                        {
                            worker = new Worker { Category = category };
                            Workers[category] = worker;
                        }
                        // Получить рабочий объект для данной category и добавить
                        // прокси клиента в список обратных вызовов
                        worker = (Worker)Workers[category];
                        var callback = ((OperationContext)current).GetCallbackChannel<IClientCallback>();
                        lock (worker.Callbacks)
                        {
                            worker.Callbacks.Add(new CallbackInfo
                            {
                                ClientCallback = callback,
                                ClientId = clientId
                            });
                        }
                    }
                }
            }).Start(OperationContext.Current);
        }

        /// <summary>Отключает клиента от подписки</summary>
        /// <param name="clientId">ID клиента</param>
        public void Disconnect(Guid clientId)
        {
            ThreadPool.QueueUserWorkItem(param =>
            {
                lock (Workers.SyncRoot)
                {
                    foreach (var callbacks in from DictionaryEntry worker in Workers
                                              select ((Worker)worker.Value).Callbacks)
                    {
                        lock (callbacks)
                        {
                            var removing =
                                callbacks.Where(cbinfo => cbinfo.ClientId.Equals(clientId)).ToList();
                            foreach (var callback in removing) callbacks.Remove(callback);
                        }
                    }
                }
            });
        }

        /// <summary>Изменение значения свойства клиентом</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        public void UpdateProperty(Guid clientId, string category, string pointname, string propname, string value,
                                   bool nocash)
        {
            CustomUpdateProperty(clientId, DateTime.Now, category, pointname, propname, value, nocash, false);
        }

        /// <summary>Изменение значения свойства внутри сервера</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="snaptime"></param>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        /// <param name="self">передача значений только для ID клиента или всем кроме</param>
        internal static void CustomUpdateProperty(Guid clientId, DateTime snaptime, string category, string pointname,
                                                 string propname,
                                                 string value,
                                                 bool nocash, bool self)
        {
            if (nocash || SetPropValue(category, pointname, propname, value))
            {
                ThreadPool.QueueUserWorkItem(param =>
                {
                    lock (Workers.SyncRoot)
                    {
                        foreach (var callbacks in from DictionaryEntry worker in Workers
                                                  where ((Worker)worker.Value).Category.Equals(category)
                                                  select ((Worker)worker.Value).Callbacks)
                        {
                            lock (callbacks)
                            {
                                var removing = new List<CallbackInfo>();
                                foreach (var callbackInfo in
                                         callbacks.Where(
                                            callbackInfo => !self && !callbackInfo.ClientId.Equals(clientId) ||
                                                            self && callbackInfo.ClientId.Equals(clientId)))
                                {
                                    try
                                    {
                                        callbackInfo.ClientCallback.PropertyUpdated(snaptime, category,
                                                                                    pointname, propname,
                                                                                    value);
                                    }
                                    catch (CommunicationObjectAbortedException)
                                    {
                                        removing.Add(callbackInfo);
                                    }
                                }
                                foreach (var callbackInfo in removing) callbacks.Remove(callbackInfo);
                            }
                        }
                    }
                });
            }
        }

    }

}
