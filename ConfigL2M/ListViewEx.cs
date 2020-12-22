using System;
using System.Collections;
using System.Windows.Forms;

namespace ConfigL2M
{
    public class ListViewEx : ListView
    {
        private const int WmHscroll = 0x0114;
        private const int WmVscroll = 0x0115;
        private const int WmKeydown = 0x0100;
        private const int WmMousewheel = 0x020A;

        public event EventHandler ScrollEvent;

        [System.Security.Permissions.SecurityPermission
            (System.Security.Permissions.SecurityAction.LinkDemand, Flags =
             System.Security.Permissions.SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmVscroll ||
                m.Msg == WmHscroll ||
                m.Msg == WmKeydown ||
                m.Msg == WmMousewheel)
                ScrollEvent?.Invoke(this, null);
            base.WndProc(ref m);
        }

        public void SetDoubleBuffered(bool value)
        {
            DoubleBuffered = value;
        }

        // Implements the manual sorting of items by columns.
        public class ListViewItemComparer : IComparer
        {
            private readonly int _col;

            public ListViewItemComparer(int column)
            {
                _col = column;
            }

            public int Compare(object x, object y)
            {
                var itemX = (ListViewItem)x;
                var itemY = (ListViewItem)y;
                if (_col < itemX.SubItems.Count && _col < itemY.SubItems.Count)
                    return string.CompareOrdinal(itemX.SubItems[_col].Text, itemY.SubItems[_col].Text);
                return 0;
            }
        }

        // Implements the manual reverse sorting of items by columns.
        public class ListViewItemReverseComparer : IComparer
        {
            private readonly int _col;

            public ListViewItemReverseComparer(int column)
            {
                _col = column;
            }

            public int Compare(object x, object y)
            {
                var itemX = (ListViewItem)x;
                var itemY = (ListViewItem)y;
                if (_col < itemX.SubItems.Count && _col < itemY.SubItems.Count)
                    return string.CompareOrdinal(itemY.SubItems[_col].Text, itemX.SubItems[_col].Text);
                return 0;
            }
        }
        // Implements the manual sorting of items last column by date.
        public class ListViewItemDateComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                var itemX = (ListViewItem)x;
                var itemY = (ListViewItem)y;
                var colX = itemX.SubItems.Count - 1;
                var colY = itemY.SubItems.Count - 1;
                var dt = DateTime.MinValue;
                if (itemX.SubItems[colX].Tag != null &&
                    itemX.SubItems[colX].Tag.GetType() == dt.GetType() &&
                    itemY.SubItems[colY].Tag != null &&
                    itemY.SubItems[colY].Tag.GetType() == dt.GetType())
                {
                    return DateTime.Compare((DateTime)itemX.SubItems[colX].Tag,
                        (DateTime)itemY.SubItems[colY].Tag);
                }
                return 0;
            }
        }

        // Implements the manual sorting of items last column by date.
        public class ListViewItemReverseDateComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                var itemX = (ListViewItem)x;
                var itemY = (ListViewItem)y;
                var colX = itemX.SubItems.Count - 1;
                var colY = itemY.SubItems.Count - 1;
                var dt = DateTime.MinValue;
                if (itemX.SubItems[colX].Tag != null &&
                    itemX.SubItems[colX].Tag.GetType() == dt.GetType() &&
                    itemY.SubItems[colY].Tag != null &&
                    itemY.SubItems[colY].Tag.GetType() == dt.GetType())
                {
                    return DateTime.Compare((DateTime)itemY.SubItems[colY].Tag,
                        (DateTime)itemX.SubItems[colX].Tag);
                }
                return 0;
            }
        }
    }
}
