namespace L2M
{
    public class AnswerData
    {
        public byte Dad { get; set; }       // байт адреса приёмника
        public byte Sad { get; set; }       // байт адреса источника
        public byte Fnc { get; set; }       // код функции при ответе
        public int Channel { get; set; }
        public int Parameter { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Time { get; set; }

        public override string ToString()
        {
            return $"{Dad} {Sad} {Fnc} {Channel} {Parameter} {Value} {Unit} {Time}";
        }
    }
}
