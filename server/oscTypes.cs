using System;
using System.Runtime.InteropServices;

namespace server
{
	static class oscTypes
	{
		public const int NUM_SCENES = 8;
		public const int BUFFER_SIZE = 256;
	}
	
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct OSCMsg
	{
		public Int32 scene;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst=40)] public char[] address;
		public float value;
		public static int Size => 40 + sizeof(Int32) + sizeof(float);
		public byte[] ToBytes()  
		{  
			IntPtr ptr = Marshal.AllocHGlobal(Size);
			Marshal.StructureToPtr(this, ptr, true);
			byte[] bytes = new byte[Size];
			Marshal.Copy(ptr, bytes, 0, Size);
			Marshal.FreeHGlobal(ptr);
			return bytes;  
		}  

		public void FromBytes(byte[] fromBytes)
		{
			IntPtr ptr = Marshal.AllocHGlobal(Size);
			Marshal.Copy(fromBytes, 0, ptr, Size);
			OSCMsg tmp = (OSCMsg)Marshal.PtrToStructure(ptr, typeof(OSCMsg));

			scene = tmp.scene;
			address = new char[40];
			for(int k = 0; k < 40; k++)
				address[k] = tmp.address[k];
			value = tmp.value;
			Marshal.FreeHGlobal(ptr);
		}
	};
}
