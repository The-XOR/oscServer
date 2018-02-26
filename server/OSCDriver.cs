using System;
using System.Threading;
using ipcServer;
using oscServer;

namespace server
{
	class OSCDriver
	{
		private OscServer oscSrv;
		private IPCServer ipc;
		private Thread readWorker;
		private volatile bool workerRunning;

		public OSCDriver()
		{
			oscSrv = null;
			ipc = null;
			readWorker = null;
			workerRunning = false;
		}

		public bool Start(string outAddress, int inPort, int outPort)
		{
			oscSrv = new OscServer(rcvCb);
			ipc = new IPCServer();
			if(oscSrv.Open(outAddress, inPort, outPort))
			{
				if(ipc.Open())
				{
					readWorker = new Thread(data => { (data as OSCDriver)?.onWaitMessages(); });
					readWorker.Start(this);
					return true;
				}
			}

			Stop();
			return false;
		}

		protected void onWaitMessages()
		{
			workerRunning = true;
			while(workerRunning)
			{
				bool rd = false;
				for(int k = 0; k < oscTypes.NUM_SCENES; k++)
				{
					OSCMsg msg = new OSCMsg();
					if(ipc.Read(k, ref msg))
					{
						rd = true;
						sendToOsc(ref msg);
					}

					if(!workerRunning)
						break;
				}
				if(!rd && workerRunning)
				{
					Thread.Sleep(20);
				}
			}
		}

		private void sendToOsc(ref OSCMsg msg)
		{
			string addr = new string(msg.address).Replace("\0", "");
			string realAddress = $"/scene{msg.scene}{addr}";
			OscMessage oscmsg = new OscMessage(realAddress, msg.value);
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"from cli {oscmsg.Address}");
			foreach(object par in oscmsg.Arguments)
			{
				System.Diagnostics.Debug.WriteLine($"  arg: {par.ToString()}");
			}
#endif
			oscSrv.Write(oscmsg);
		}

		private void rcvCb(OscPacket packet)
		{
			if(packet.IsBundle)
			{
				OscBundle bndl = packet as OscBundle;
				foreach(OscMessage msg in bndl.Messages)
				{
					sendToClient(msg);
				}
			} else
				sendToClient(packet as OscMessage);
		}

		public void Stop()
		{
			if(readWorker != null)
			{
				workerRunning = false;
				readWorker?.Join();
				readWorker = null;
			}

			ipc?.Close();
			ipc = null;

			oscSrv?.Dispose();
			oscSrv = null;
		}

		private void sendToClient(OscMessage rmsg)
		{
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"from osc {rmsg.Address}");
			foreach(object par in rmsg.Arguments)
			{
				System.Diagnostics.Debug.WriteLine($"  arg: {par.ToString()}");
			}
#endif
			if(rmsg.Address.Length > 8 && rmsg.Address.StartsWith("/scene"))
			{
				OSCMsg msg;
				if(int.TryParse(rmsg.Address.Substring(6, 1), out msg.scene))
				{
					if(msg.scene > 0 && msg.scene <= oscTypes.NUM_SCENES)
					{
						msg.address = rmsg.Address.Substring(7).PadRight(40, '\0').ToCharArray();

						foreach(object par in rmsg.Arguments)
						{
							if(par is float)
							{
								msg.value = (float)par;
								ipc.Write(ref msg);
							}
						}
					}
				}
			}
		}
	}
}
