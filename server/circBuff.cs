using System;
using System.IO.MemoryMappedFiles;
using server;

namespace ipcServer
{
	public class circBuffer
	{
		private MemoryMappedViewAccessor mapAccess;

		private readonly int WR_PTR_POS;
		private readonly int P0;
		private readonly int PN;
		private int rdPtr;
		public static int bufferOverhead { get; } = sizeof(Int32);

		public void Clear()
		{
			rdPtr = wrPtr;
		}

		public circBuffer(int bufferSize, MemoryMappedViewAccessor acc, int offset)
		{
			WR_PTR_POS = offset;
			P0 = WR_PTR_POS + bufferOverhead;   // posizione 0 memoria effettiva (dove salvare e leggere i dati, dopo i primi 8 bytes riservati ai puntatori rd/wr)
			PN = P0 + bufferSize - bufferOverhead;
			mapAccess = acc;
			rdPtr = wrPtr = P0;
		}

		public void Close()
		{
			mapAccess = null;
		}
	
		private int wrPtr
		{
			get { return mapAccess.ReadInt32(WR_PTR_POS) ;}
			set { mapAccess.Write(WR_PTR_POS, value) ;}
		}

		private int incPtr(int ptr)
		{
			if(++ptr >= PN)
				ptr = P0;

			return ptr;
		}

		private int Put(byte b, int ptr)
		{
			mapAccess.Write(ptr, b);
			return incPtr(ptr);
		}
	
		private byte Get()
		{
			byte rv = mapAccess.ReadByte(rdPtr);
			rdPtr = incPtr(rdPtr);
			return rv;
		}
		
		public void WriteChunk(ref OSCMsg msg)
		{
			byte[] data = msg.ToBytes();
			int cur_wrPtr = wrPtr;
			foreach(byte b in data)
				cur_wrPtr = Put(b, cur_wrPtr);
			wrPtr = cur_wrPtr;	// finalizza IN SOLIDO il chunk appena letto
		}

		public bool ReadChunk(ref OSCMsg dest)
		{
			if(rdPtr != wrPtr)
			{
				byte[] buff = new byte[OSCMsg.Size];
				for(int k = 0; k < buff.Length; k++)
					buff[k] = Get();
				dest.FromBytes(buff);
				return true;
			}

			return false;
		}

	}
}