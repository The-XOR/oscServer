using System;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;
using server;

namespace ipcServer
{
	public class IPCServer
	{	
		protected const int SERVERUP_POS = 0;      // (Int32) : indica se il server e' up o down
		private const int START_OF_BUFFER = SERVERUP_POS + sizeof(Int32);    // prima locazione di memoria utile per il buffer circolare
		private MemoryMappedFile m_hMapFile;      // Handle to the mapped memory file
		protected MemoryMappedViewAccessor mapAccess;
		protected circBuffer[] oscToClientBuff;	// dati in arrivo da remoto
		protected circBuffer[] clientToOscBuff;	// dati destinati ai client
		protected MemoryMappedFile createMapFile(string memName, int rawSize, MemoryMappedFileSecurity security) => 
			MemoryMappedFile.CreateNew(memName, rawSize, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, security, HandleInheritability.Inheritable);

		public IPCServer()
		{
			oscToClientBuff = clientToOscBuff = null;
			mapAccess = null;
			m_hMapFile = null;
		}
				
		private MemoryMappedFileSecurity MMFSecurity()
		{
			MemoryMappedFileSecurity security = new MemoryMappedFileSecurity();
			security.SetAccessRule(new AccessRule<MemoryMappedFileRights>(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MemoryMappedFileRights.ReadWrite, AccessControlType.Allow));
			return security;
		}

		public bool Open()
		{
			int b_l = oscTypes.BUFFER_SIZE * OSCMsg.Size + circBuffer.bufferOverhead;
			int rawsize = oscTypes.NUM_SCENES * 2 * b_l + sizeof(Int32) /*flag SERVER_POS*/;
			bool opened;
			try
			{
				m_hMapFile = createMapFile("OSC_mem", rawsize, MMFSecurity());
				opened = true;
			} catch(Exception)
			{
				opened = false;
			}

			if(opened)
			{
				mapAccess = m_hMapFile.CreateViewAccessor(0, rawsize);
				oscToClientBuff = new circBuffer[oscTypes.NUM_SCENES];
				clientToOscBuff = new circBuffer[oscTypes.NUM_SCENES];
				for(int k = 0; k < oscTypes.NUM_SCENES; k++)
				{
					int offset = b_l * 2 * k;
					clientToOscBuff[k] = new circBuffer(b_l, mapAccess, START_OF_BUFFER+offset);
					oscToClientBuff[k] = new circBuffer(b_l, mapAccess, START_OF_BUFFER + b_l+offset);
				}
				mapAccess.Write(SERVERUP_POS, 0x01);
			}

			return opened;
		}

		public void Close()
		{			
			for(int k = 0; k < oscTypes.NUM_SCENES; k++)
			{
				clientToOscBuff?[k].Close();
				oscToClientBuff?[k].Close();
			}
			oscToClientBuff = clientToOscBuff = null;

			if(mapAccess != null)
			{
				mapAccess.Write(SERVERUP_POS, 0);
				MemoryMappedViewAccessor handle = mapAccess;
				mapAccess = null;
				handle?.Dispose();
			}

			// Close the file handle
			if(m_hMapFile != null)
			{
				MemoryMappedFile handle = m_hMapFile;
				m_hMapFile = null;
				handle?.Dispose();
			}
		}

		public void Write(ref OSCMsg msg)
		{
			oscToClientBuff[msg.scene-1].WriteChunk(ref msg);
		}

		public bool Read(int scene, ref OSCMsg dest)
		{
			return clientToOscBuff[scene].ReadChunk(ref dest);
		}
	}
}
