namespace L2M
{
    public class RequestData
    {
        public byte Dad { get; set; }
        public byte Sad { get; set; }
        public int Channel { get; set; }
        public LogikaParam ParameterKind { get; set; }
        public int Parameter { get; set; }
        public int ArrayIndexNumber { get; set; }
        public bool Archived { get; set; }
        public byte NodeAddr { get; set; }
        public ModbusTable ModbusTable { get; set; }
        public ushort StartAddr { get; set; }
        public string FormatData { get; set; }
        public int AnswerWait { get; set; }

        public string AsParameter { get => $"Logika {Dad}.{Channel:00}.{Parameter:000}"; }
        public string AsArrayIndex { get => $"Logika {Dad}.{Channel:00}.{Parameter:000}[{ArrayIndexNumber:00}]"; }
        public string AsAddress { get => $"{NodeAddr}:{ModbusTable}:{StartAddr}"; }
    }
}
