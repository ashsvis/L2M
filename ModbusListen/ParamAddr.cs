namespace L2M
{
    public struct ParamAddr
    {
        readonly byte Node;
        readonly ushort Address;

        public ParamAddr(byte node, ushort address)
        {
            Node = node;
            Address = address;
        }
    }
}
