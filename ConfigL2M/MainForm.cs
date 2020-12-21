using DataEventClient;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ConfigL2M
{
    public partial class MainForm : Form
    {
        private readonly EventClient locEvClient;

        private readonly Dictionary<string, string> config = new Dictionary<string, string>();
        private readonly Dictionary<string, string> fetching = new Dictionary<string, string>();

        public MainForm()
        {
            InitializeComponent();
            locEvClient = new EventClient();
        }

        private void MainForm_Load(object sender, System.EventArgs e)
        {
            locEvClient.Connect(new[] { "config", "fetching", "archives" }, PropertyUpdate, ShowError, UpdateLocalConnectionStatus);
        }

        private void UpdateLocalConnectionStatus(Guid clientId, ClientConnectionStatus status)
        {
            var method = new MethodInvoker(() =>
            {
                switch (status)
                {
                    case ClientConnectionStatus.Opened:
                        //scServerConnected.State = true;
                        Text = "Подключение к серверу событий установлено.";
                        break;
                    case ClientConnectionStatus.Opening:
                        //scServerConnected.State = null;
                        Text = "Подключение к серверу событий...";
                        config.Clear();
                        fetching.Clear();
                        break;
                    default:
                        //scServerConnected.State = false;
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
                        var key = $"{pointname}.{propname}";
                        if (!fetching.ContainsKey(key))
                            fetching.Add(key, value);
                        else
                            fetching[key] = value;

                        Text = $"{category}:{key}={value}";

                        //var found = false;
                        //foreach (var lvi in lvValues.Items.Cast<ListViewItem>())
                        //{
                        //    if ($"{lvi.Tag}" == key)
                        //    {
                        //        found = true;
                        //        lvi.SubItems[1].Text = value;
                        //        break;
                        //    }
                        //}
                        //if (!found && Path.GetDirectoryName(pointname) == $"{selectedPath}")
                        //{
                        //    var lvi = new ListViewItem(Path.GetFileName(key));
                        //    lvi.SubItems.Add(value);
                        //    lvi.Tag = key;
                        //    lvValues.Items.Add(lvi);
                        //    lvValues.Sort();
                        //}

                    });
                    if (InvokeRequired)
                        BeginInvoke(method1);
                    else
                        method1();
                    break;
                case "archives":
                    var method2 = new MethodInvoker(() =>
                    {
                        //listBox2.Items.Insert(0, $"{pointname} {propname} {value}");
                    });
                    if (InvokeRequired)
                        BeginInvoke(method2);
                    else
                        method2();
                    break;
                case "config":
                    var method3 = new MethodInvoker(() =>
                    {
                        switch (pointname.ToLower())
                        {
                            case "add":
                                //var treeNodes = propname.StartsWith("root") ? tvSources : tvNodes;
                                //var tree = treeNodes.Nodes;
                                //var nodes = tree.Find(propname, true);
                                //if (nodes.Length == 0)
                                //{
                                //    treeNodes.BeginUpdate();
                                //    try
                                //    {
                                //        foreach (var item in propname.Split('\\'))
                                //        {
                                //            nodes = tree.Find(item, false);
                                //            if (nodes.Length == 0)
                                //            {
                                //                var node = new TreeNode(item) { Name = item };
                                //                tree.Add(node);
                                //                tree = node.Nodes;
                                //            }
                                //            else
                                //                tree = nodes[0].Nodes;
                                //        }
                                //        treeNodes.Sort();
                                //    }
                                //    finally
                                //    {
                                //        treeNodes.EndUpdate();
                                //    }
                                //}
                                break;
                        }
                        // для работы списка свойств
                        var key = $"{pointname}\\{propname}";
                        if (!config.ContainsKey(key))
                            config.Add(key, value);
                        else
                            config[key] = value;
                    });
                    if (InvokeRequired)
                        BeginInvoke(method3);
                    else
                        method3();
                    break;
            }
        }

    }
}
