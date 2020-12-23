using DataEventClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;
using static ConfigL2M.ListViewEx;

namespace ConfigL2M
{
    public partial class MainForm : Form
    {
        private readonly EventClient locEvClient;

        //private readonly Dictionary<string, string> config = new Dictionary<string, string>();
        //private readonly Dictionary<string, string> fetching = new Dictionary<string, string>();

        private readonly Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>();

        private int lastColumn = 1;

        public MainForm()
        {
            InitializeComponent();
            locEvClient = new EventClient();
            lvList.SetDoubleBuffered(true);
            lvList.ListViewItemSorter = new ListViewItemComparer(lastColumn);
        }

        private void MainForm_Load(object sender, System.EventArgs e)
        {
            locEvClient.Connect(new[] { "config", "fetching", "archives" }, PropertyUpdate, ShowError, UpdateLocalConnectionStatus);
            var fileName = "L2M.xml";
            if (File.Exists(fileName))
                LoadConfig(fileName);
        }

        private void LoadConfig(string fileName)
        {
            // чтение конфигурационного файла
            var xdoc = XDocument.Load(fileName);
            // чтение параметров для опрашивающих потоков
            foreach (XElement fetchingTcp in xdoc.Element("Config").Element("Fetching").Elements("ChannelTcp"))
            {
                //ReadConfigParameters(fetchingTcp, out ipAddress, out ipPort, out sendTimeout, out receiveTimeout);
                //var parameters = FillParameters(fetchingTcp);
            }
        }

        private void UpdateLocalConnectionStatus(Guid clientId, ClientConnectionStatus status)
        {
            var method = new MethodInvoker(() =>
            {
                switch (status)
                {
                    case ClientConnectionStatus.Opened:
                        Text = "Подключение к серверу событий установлено.";
                        break;
                    case ClientConnectionStatus.Opening:
                        Text = "Подключение к серверу событий...";
                        //config.Clear();
                        //fetching.Clear();
                        break;
                    default:
                        break;
                }
            });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
        }

        private void ShowError(string errormessage)
        {
            var method = new MethodInvoker(() =>
            {
                Text = errormessage;
            });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
        }

        private void PropertyUpdate(DateTime servertime, string category, string pointname, string propname, string value)
        {
            switch (category.ToLower())
            {
                case "fetching":
                    var method1 = new MethodInvoker(() =>
                    {
                        // для работы списка свойств
                        var key = $"{pointname}:{propname}";
                        //if (!fetching.ContainsKey(key))
                        //    fetching.Add(key, value?.TrimEnd());
                        //else
                        //    fetching[key] = value?.TrimEnd();

                        var keys = key.Split(':');
                        var node = keys[0];
                        var table = keys[1];
                        var index = keys[2];

                        #region Добавление групп просмотра

                        var groupKey = $"Modbus node: {node}, {table}";

                        if (!groups.ContainsKey(groupKey))
                            groups.Add(groupKey, new ListViewGroup(groupKey));

                        var group = groups[groupKey];

                        if (!lvList.Groups.Contains(group))
                            lvList.Groups.Add(group);

                        #endregion

                        ListViewItem lvi = lvList.FindItemWithText(key);
                        var vals = (value ?? string.Empty).Split('\t');
                        if (lvi == null)
                        {
                            lvi = new ListViewItem(key);
                            lvi.SubItems.Add($"{ModifyToModbusRegisterAddress(ushort.Parse(index), table)}");
                            lvi.SubItems.Add(vals[0]);
                            lvi.SubItems.Add(vals.Length > 1 ? vals[1] : "");
                            lvi.SubItems.Add(vals.Length > 2 ? vals[2] : "");
                            lvi.SubItems.Add(propname);

                            lvi.Group = group;

                            lvList.Items.Add(lvi);
                        }
                        else
                        {
                            lvi.SubItems[2].Text = vals[0];
                            lvi.SubItems[3].Text = vals.Length > 1 ? vals[1] : "";
                            lvi.SubItems[4].Text = vals.Length > 2 ? vals[2] : "";
                        }

                    });
                    if (InvokeRequired)
                        BeginInvoke(method1);
                    else
                        method1();
                    break;
                case "archives":
                    var method2 = new MethodInvoker(() =>
                    {

                    });
                    if (InvokeRequired)
                        BeginInvoke(method2);
                    else
                        method2();
                    break;
                case "config":
                    //var method3 = new MethodInvoker(() =>
                    //{
                    //    switch (pointname.ToLower())
                    //    {
                    //        case "add":
                    //             break;
                    //    }
                    //    // для работы списка свойств
                    //    var key = $"{pointname}\\{propname}";
                    //    if (!config.ContainsKey(key))
                    //        config.Add(key, value);
                    //    else
                    //        config[key] = value;
                    //});
                    //if (InvokeRequired)
                    //    BeginInvoke(method3);
                    //else
                    //    method3();
                    break;
            }
        }

        public static ushort ModifyToModbusRegisterAddress(ushort startAddr, string table)
        {
            switch (table)
            {
                case "Coils":
                    return Convert.ToUInt16(1 + startAddr);       // coils
                case "Contacts":
                    return Convert.ToUInt16(10001 + startAddr);   // contacts
                case "Holdings":
                    return Convert.ToUInt16(40001 + startAddr);   // holdings
                case "Inputs":
                    return Convert.ToUInt16(30001 + startAddr);   // inputs
            }
            throw new NotImplementedException();
        }

        private void lvList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (lastColumn != e.Column)
            {
                lvList.ListViewItemSorter = new ListViewItemComparer(e.Column);
                lastColumn = e.Column;
            }
            else
            {
                if (lvList.ListViewItemSorter is ListViewItemComparer)
                    lvList.ListViewItemSorter = new ListViewItemReverseComparer(e.Column);
                else
                    lvList.ListViewItemSorter = new ListViewItemComparer(e.Column);
            }
            if (lvList.FocusedItem != null)
                lvList.FocusedItem.EnsureVisible();
        }

        private void lvList_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var lvi = new ListViewItem();
            e.Item = lvi;
        }
    }
}
