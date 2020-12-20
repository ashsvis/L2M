using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace L2M
{
    public static class Modbus
    {
        private static readonly ushort[,] registers = new ushort[247, 50000];

        private static readonly object locker = new object();

        private static readonly ConcurrentDictionary<ParamAddr, string> writers = new ConcurrentDictionary<ParamAddr, string>();

        public static void SetParamValue(ParamAddr param, string value)
        {
            writers.AddOrUpdate(param, value, (k, v) => value);
        }

        public static bool ParamsToWriteExists()
        {
            return writers.Count > 0;
        }

        public static string GetParamValue(ParamAddr param)
        {
            if (writers.TryGetValue(param, out string value))
            {
                writers.TryRemove(param, out string stub);
                return value;
            }
            return null;
        }

        public static void PrintInputRegisters(int node, int top)
        {
            lock (locker)
            {
                // диапазон 4хххх - для holding регистров
                for (var i = 30000; i < 30010; i++)
                {
                    Console.SetCursorPosition(0, top++);
                    Console.Write(Swap(registers[node - 1, i]));
                }
            }
        }

        public static object TypedValueFromRegistersArray(byte node, ushort index, Type type)
        {
            var list = new List<byte>();
            var key = type.ToString();
            switch (key)
            {
                case "System.Int16":
                    return Convert.ToInt16(GetRegisterValue(node, index));
                case "System.UInt16":
                    return GetRegisterValue(node, index);
                case "System.Int32":
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, index)));
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, (ushort)(index + 1))));
                    return BitConverter.ToInt32(list.ToArray(), 0);
                case "System.UInt32":
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, index)));
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, (ushort)(index + 1))));
                    return BitConverter.ToUInt32(list.ToArray(), 0);
                case "System.Single":
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, index)));
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, (ushort)(index + 1))));
                    return BitConverter.ToSingle(Swap(list, 0, "DCBA"), 0);
                case "System.Double":
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, index)));
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, (ushort)(index + 1))));
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, (ushort)(index + 2))));
                    list.AddRange(BitConverter.GetBytes(GetRegisterValue(node, (ushort)(index + 3))));
                    return BitConverter.ToDouble(list.ToArray(), 0);
            }
            throw new NotImplementedException();
        }

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

        private static byte[] Swap(IEnumerable<byte> buff, int startIndex, string typeSwap)
        {
            var list = buff.Skip(startIndex).ToArray();
            if (list.Length == 2)
            {
                switch (typeSwap)
                {
                    case "AB":
                        return new byte[] { list[0], list[1] };
                    case "BA":
                        return new byte[] { list[1], list[0] };
                    default:
                        return list;
                }
            }
            else if (list.Length == 4)
            {
                switch (typeSwap)
                {
                    case "ABCD":
                        return new byte[] { list[0], list[1], list[2], list[3] };
                    case "CDAB":
                        return new byte[] { list[2], list[3], list[0], list[1] };
                    case "BADC":
                        return new byte[] { list[1], list[0], list[3], list[2] };
                    case "DCBA":
                        return new byte[] { list[3], list[2], list[1], list[0] };
                    default:
                        return list;
                }
            }
            else if (list.Length == 8)
            {
                switch (typeSwap)
                {
                    case "ABCDEFGH":
                        return new byte[] { list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7] };
                    case "GHEFCDAB":
                        return new byte[] { list[6], list[7], list[4], list[5], list[2], list[3], list[0], list[1] };
                    case "BADCFEHG":
                        return new byte[] { list[1], list[0], list[3], list[2], list[5], list[4], list[7], list[6] };
                    case "HGFEDCBA":
                        return new byte[] { list[7], list[6], list[5], list[4], list[3], list[2], list[1], list[0] };
                    default:
                        return list;
                }
            }
            else
                return list;
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
