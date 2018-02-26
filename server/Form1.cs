using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace server
{
	public partial class Form1 : Form
	{
		private OSCDriver osc;

		public Form1()
		{
			InitializeComponent();
			notifyIcon1.ContextMenu = new ContextMenu();
			notifyIcon1.ContextMenu.MenuItems.AddRange(new MenuItem[] { new MenuItem("&Setup", (sender, e) => setup()), new MenuItem("&Exit", (sender, e) => this.Close()) });
			notifyIcon1.Text = "OSCServer";
			notifyIcon1.Visible = true;
		}

		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			stop();
		}

		private void stop()
		{
			osc?.Stop();
			osc = null;
		}

		private void start()
		{
			int inP, outP;
			if(osc == null && int.TryParse(outPort.Text, out outP) && int.TryParse(inPort.Text, out inP))
			{
				osc = new OSCDriver();
				if(!osc.Start(destAddress.Text, inP, outP))
					osc = null;
				else
				{
					Setup s = new Setup();
					s.destAddress = destAddress.Text;
					s.destPort = outPort.Text;
					s.listenPort = inPort.Text;
					s.Save();
				}
			}
		}

		private void setup()
		{
			Show();
		}

		private void button2_Click(object sender, EventArgs e) //stop
		{
			stop();
			update_interface();
		}

		private void update_interface()
		{
			bool opened = (osc != null);
			button2.Enabled = opened;
			button1.Enabled = destAddress.Enabled = outPort.Enabled = inPort.Enabled = !opened;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			start();
			update_interface();
			if(osc != null)
				Hide();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			Setup s = new Setup();
			s.Load();
			destAddress.Text = s.destAddress;
			outPort.Text = s.destPort;
			inPort.Text = s.listenPort;
			show_ip();
		}

		private void show_ip()
		{
			string hn = Dns.GetHostName();
			string ipn = "";
			var host = Dns.GetHostEntry(hn);
			foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
			{
				var addr = ni.GetIPProperties().GatewayAddresses.FirstOrDefault();
				if(addr != null && !addr.Address.ToString().Equals("0.0.0.0"))
				{
					if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
					{
						foreach(UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
						{
							if(ip.Address.AddressFamily == AddressFamily.InterNetwork)
							{
								ipn = ip.Address.ToString();
							}
						}
					}
				}
			}

			ipAddress.Text = $"{hn} ({ipn})";
		}
	}
}
