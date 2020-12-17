using System;

namespace L2M
{
    public static class Modbus
    {
        private static readonly ushort[,] registers = new ushort[247, 50000];

        private static readonly object locker = new object();

        public static ushort GetRegisterValue(byte node, ushort index)
        {
            lock (locker)
            {
                return registers[node - 1, index - 1];
            }
        }

        public static void SetRegisterValue(byte node, ushort index, ushort value)
        {
            lock (locker)
            {
                registers[node - 1, index - 1] = value;
            }
        }

        public static ushort Swap(ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            var buff = bytes[0];
            bytes[0] = bytes[1];
            bytes[1] = buff;
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static ushort ModifyToModbusRegisterAddress(ushort startAddr, ModbusTable funcCode)
        {
            switch (funcCode)
            {
                case ModbusTable.Coils:
                    return Convert.ToUInt16(1 + startAddr);       // coils
                case ModbusTable.Contacts:
                    return Convert.ToUInt16(10001 + startAddr);   // contacts
                case ModbusTable.Holdings:
                    return Convert.ToUInt16(40001 + startAddr);   // holdings
                case ModbusTable.Inputs:
                    return Convert.ToUInt16(30001 + startAddr);   // inputs
            }
            throw new NotImplementedException();
        }

    }
}
