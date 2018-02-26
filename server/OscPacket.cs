using System;
using System.Collections.Generic;
using System.Text;

namespace oscServer
{
	public abstract class OscPacket
	{
		public static OscPacket FromBytes(byte[] bytes)
		{
			if(bytes[0] == '#')
				return fromBundle(bytes);
			else
				return fromMsg(bytes);
		}
		public abstract byte[] ToBytes();
		public abstract bool IsBundle { get; }

		private static int getNextComma(byte[] msg, int offset)
		{
			for(int k = offset; k < msg.Length; k++)
			{
				if(msg[k] == ',')
					return k;
			}

			return -1;
		}

		private static int align(int index)
		{
			while(index % 4 != 0)
				index++;

			return index;
		}

		private static OscMessage fromMsg(byte[] msg)
		{
			List<object> arguments = new List<object>();
			List<object> mainArray = arguments; // used as a reference when we are parsing arrays to get the main array back

			string address = getAddress(msg, 0);
			if(address == null)
				return null;
			int index = getNextComma(msg, address.Length);
			if(index % 4 != 0)
				return null;	// invalid packet

			char[] types = getTypes(msg, index);
			if(types == null)
				return null;

			index += types.Length;

			index = align(index);

			bool commaParsed = false;
			foreach(char type in types)
			{
				if(type == ',' && !commaParsed)
				{
					commaParsed = true;
					continue;
				}

				switch(type)
				{
					case ('\0'):
						break;

					case ('i'):
						int intVal = getInt(msg, index);
						arguments.Add(intVal);
						index += 4;
						break;

					case ('f'):
						float floatVal = getFloat(msg, index);
						arguments.Add(floatVal);
						index += 4;
						break;

					case ('s'):
						string stringVal = getString(msg, index);
						if(stringVal == null)
							return null;
						arguments.Add(stringVal);
						index += stringVal.Length;
						break;

					case ('b'):
						byte[] blob = getBlob(msg, index);
						arguments.Add(blob);
						index += 4 + blob.Length;
						break;

					case ('h'):
						Int64 hval = getLong(msg, index);
						arguments.Add(hval);
						index += 8;
						break;

					case ('t'):
						UInt64 sval = getULong(msg, index);
						arguments.Add(new Timetag(sval));
						index += 8;
						break;

					case ('d'):
						double dval = getDouble(msg, index);
						arguments.Add(dval);
						index += 8;
						break;

					case ('S'):
						string SymbolVal = getString(msg, index);
						if(SymbolVal == null)
							return null;
						arguments.Add(new Symbol(SymbolVal));
						index += SymbolVal.Length;
						break;

					case ('c'):
						char cval = getChar(msg, index);
						arguments.Add(cval);
						index += 4;
						break;

					case ('r'):
						RGBA rgbaval = getRGBA(msg, index);
						arguments.Add(rgbaval);
						index += 4;
						break;

					case ('m'):
						Midi midival = getMidi(msg, index);
						arguments.Add(midival);
						index += 4;
						break;

					case ('T'):
						arguments.Add(true);
						break;

					case ('F'):
						arguments.Add(false);
						break;

					case ('N'):
						arguments.Add(null);
						break;

					case ('I'):
						arguments.Add(double.PositiveInfinity);
						break;

					case ('['):
						if(arguments != mainArray)
							return null;
						arguments = new List<object>(); // make arguments point to a new object array
						break;

					case (']'):
						mainArray.Add(arguments); // add the array to the main array
						arguments = mainArray; // make arguments point back to the main array
						break;

					default:
						return null;
				}

				index = align(index);
			}

			return new OscMessage(address, arguments.ToArray());
		}

		private static byte[] subArray(byte[] src, int index, int length)
		{
			byte[] result = new byte[length];
			Array.Copy(src, index, result, 0, length);
			return result;
		}
				
		private static OscBundle fromBundle(byte[] bundle)
		{
			UInt64 timetag;
			List<OscMessage> messages = new List<OscMessage>();

			int index = 0;

			var bundleTag = Encoding.ASCII.GetString(subArray(bundle, 0, 8));
			index += 8;

			timetag = getULong(bundle, index);
			index += 8;

			if(bundleTag != "#bundle\0")
				return null;

			while(index < bundle.Length)
			{
				int size = getInt(bundle, index);
				index += 4;

				byte[] messageBytes = subArray(bundle, index, size);
				var message = fromMsg(messageBytes);

				messages.Add(message);

				index += size;
				index = align(index);
			}

			return new OscBundle(timetag, messages.ToArray());
		}
		
		private static string getAddress(byte[] msg, int index)
		{
			int i = index;
			string address = null;
			for(; i < msg.Length; i += 4)
			{
				if(msg[i] == ',')
				{
					if(i == 0)
						return "";

					address = Encoding.ASCII.GetString(subArray(msg, index, i - 1));
					break;
				}
			}

			if(i >= msg.Length && address == null)
				return null;

			return address.Replace("\0", "");
		}

		private static byte[] reverseArray(byte[] src, int index, int size)
		{
			byte[] reversed = new byte[size];
			for(int k = 0; k < size; k++)
				reversed[size - 1 - k] = src[index + k];
			return reversed;
		}

		private static char[] getTypes(byte[] msg, int index)
		{
			int i = index + 4;
			char[] types = null;

			for(; i < msg.Length; i += 4)
			{
				if(msg[i - 1] == 0)
				{
					types = Encoding.ASCII.GetChars(subArray(msg, index, i - index));
					break;
				}
			}

			if(i >= msg.Length && types == null)
				return null;

			return types;
		}

		private static int getInt(byte[] msg, int index)
		{
			return (msg[index] << 24) + (msg[index + 1] << 16) + (msg[index + 2] << 8) + (msg[index + 3] << 0);
		}

		private static float getFloat(byte[] msg, int index)
		{
			return BitConverter.ToSingle(reverseArray(msg, index, 4), 0);
		}

		private static string getString(byte[] msg, int index)
		{
			string output = null;
			int i = index + 4;
			for(; (i - 1) < msg.Length; i += 4)
			{
				if(msg[i - 1] == 0)
				{
					output = Encoding.ASCII.GetString(subArray(msg, index, i - index));
					break;
				}
			}

			if(i >= msg.Length && output == null)
				return null;

			return output.Replace("\0", "");
		}

		private static byte[] getBlob(byte[] msg, int index)
		{
			int size = getInt(msg, index);
			return subArray(msg, index + 4, size);
		}

		private static UInt64 getULong(byte[] msg, int index)
		{
			UInt64 val = ((UInt64)msg[index] << 56) + ((UInt64)msg[index + 1] << 48) + ((UInt64)msg[index + 2] << 40) + ((UInt64)msg[index + 3] << 32)
					+ ((UInt64)msg[index + 4] << 24) + ((UInt64)msg[index + 5] << 16) + ((UInt64)msg[index + 6] << 8) + ((UInt64)msg[index + 7] << 0);
			return val;
		}

		private static Int64 getLong(byte[] msg, int index)
		{
			return  BitConverter.ToInt64(reverseArray(msg, index, 8), 0);
		}

		private static double getDouble(byte[] msg, int index)
		{		
			return BitConverter.ToDouble(reverseArray(msg, index, 8), 0);
		}

		private static char getChar(byte[] msg, int index)
		{
			return (char)msg[index + 3];
		}

		private static RGBA getRGBA(byte[] msg, int index)
		{
			return new RGBA(msg[index], msg[index + 1], msg[index + 2], msg[index + 3]);
		}

		private static Midi getMidi(byte[] msg, int index)
		{
			return new Midi(msg[index], msg[index + 1], msg[index + 2], msg[index + 3]);
		}

		protected static byte[] setInt(int value)
		{
			var bytes = BitConverter.GetBytes(value);
			return reverseArray(bytes, 0, 4);
		}

		protected static byte[] setFloat(float value)
		{
			var bytes = BitConverter.GetBytes(value);
			return reverseArray(bytes, 0, 4);
		}

		protected static byte[] setString(string value)
		{
			int len = value.Length + (4 - value.Length % 4);
			if(len <= value.Length) len = len + 4;

			byte[] msg = new byte[len];

			var bytes = Encoding.ASCII.GetBytes(value);
			bytes.CopyTo(msg, 0);

			return msg;
		}

		protected static byte[] setBlob(byte[] value)
		{
			int len = value.Length + 4;
			len = len + (4 - len % 4);

			byte[] msg = new byte[len];
			byte[] size = setInt(value.Length);
			size.CopyTo(msg, 0);
			value.CopyTo(msg, 4);
			return msg;
		}

		protected static byte[] setLong(Int64 value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			return reverseArray(bytes, 0, 8);
		}

		protected static byte[] setULong(UInt64 value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			return reverseArray(bytes, 0, 8);
		}

		protected static byte[] setDouble(double value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			return reverseArray(bytes, 0, 8);
		}

		protected static byte[] setChar(char value)
		{
			return new byte[4] {0, 0, 0, (byte)value};
		}

		protected static byte[] setRGBA(RGBA value)
		{
			return new byte[4] {value.R, value.G, value.B, value.A};
		}

		protected static byte[] setMidi(Midi value)
		{
			return new byte[4] {value.Port, value.Status, value.Data1, value.Data2};
		}
		protected int alignedStringLength(string val)
		{
			int len = val.Length + (4 - val.Length % 4);
			if (len <= val.Length)
				len += 4;

			return len;
		}

	}
}
