using System;
using System.Net.Sockets;
using System.Linq;
using System.Net;

namespace oscServer
{
	public class OscServer : IDisposable
	{
		#region IDisposable Members
		public void Dispose()
		{
			_dispose(true);
			GC.SuppressFinalize(this);
		}

		private SDispose _disposed = new SDispose();
		private void _dispose(bool disposing)
		{
			if(_disposed.CanDispose())
			{
				if(disposing)
				{
					close();
				}
			}
		}

		~OscServer()
		{
			_dispose(false);
		}
		#endregion

		public delegate void OscPacketCallback(OscPacket packet);
		private Socket outSkt;
		private IPEndPoint outEndPoint;
		private IPEndPoint inEndPoint;
		private UdpClient udpListen;
		private OscPacketCallback listenerCallback;

		public OscServer(OscPacketCallback cb)
		{
			listenerCallback = cb;
			outSkt = null;
			inEndPoint = outEndPoint = null;
			udpListen = null;
		}

		public bool Open(string outAddress, int inPort = 9000, int outPort = 9001)
		{
			if(open_out(outAddress, outPort))
				return open_in(inPort);

			return false;
		}

		public void Write(OscPacket pkt)
		{
			outSkt.SendTo(pkt.ToBytes(), outEndPoint);
		}

		private void close()
		{
			outSkt?.Close();
			outSkt = null;

			outEndPoint = null;
		}

		private bool validateIPv4(string ipString)
		{
			if(!string.IsNullOrWhiteSpace(ipString))
			{
				string[] splitValues = ipString.Split('.');
				if(splitValues.Length == 4)
				{
					byte tempForParsing;
					return splitValues.All(r => byte.TryParse(r, out tempForParsing));
				}
			}

			return false;
		}

		private bool open_out(string outAddress, int outPort)
		{
			if(validateIPv4(outAddress))
			{
				string[] splitValues = outAddress.Split('.');
				byte[] addr = new byte[4];
				for(int k = 0; k < 4; k++)
				{
					addr[k] = byte.Parse(splitValues[k]);
				}
				outEndPoint = new IPEndPoint(new IPAddress(addr), outPort);
			} else
			{
				var addresses = System.Net.Dns.GetHostAddresses(outAddress);
				if(addresses.Length > 0)
					outEndPoint = new IPEndPoint(addresses[0], outPort);
			}

			if(outEndPoint != null)
			{
				outSkt = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			}

			return outSkt != null;
		}

		private bool open_in(int port)
		{
			try
			{
				udpListen = new UdpClient(port);
			} catch
			{
				udpListen = null;
			}

			if(udpListen != null)
			{
				inEndPoint = new IPEndPoint(IPAddress.Any, 0);
				AsyncCallback callBack = cb_receive;
				udpListen.BeginReceive(callBack, null);
			}

			return udpListen != null;
		}

		private void cb_receive(IAsyncResult result)
		{
			byte[] bytes = null;
			try
			{
				bytes = udpListen.EndReceive(result, ref inEndPoint);
			} catch
			{
				bytes = null;
			}

			if(bytes != null && bytes.Length > 0)
			{
				OscPacket pkt = OscPacket.FromBytes(bytes);
				if(pkt != null)
					listenerCallback?.Invoke(pkt);
			}

			if(!_disposed.IsDisposed)
			{
				// Setup next async event
				AsyncCallback callBack = cb_receive;
				udpListen.BeginReceive(callBack, null);
			}
		}
	}
}