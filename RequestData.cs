namespace L2M
{
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
        public int AnswerWait { get; set; }
    }
}
