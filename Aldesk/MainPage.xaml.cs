using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace Aldesk;

public class DeskListener
{
	public CustomDebugger customDebugger { get; set; }
	public MainPage mainPage { get; set; }

	public DeskListener(MainPage mainPage, CustomDebugger customDebugger)
	{
		this.mainPage = mainPage;
		this.customDebugger = customDebugger;
	}

	public async void DoWork()
	{
		await ListenServer();
	}

	public async Task ListenServer()
	{
		try
		{
			mainPage.Listening = true;
			IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
			var ipbytes = Encoding.ASCII.GetBytes(mainPage.IpAdress);
			IPAddress ipAddr = new IPAddress(ipbytes);
			IPEndPoint localEndPoint = new IPEndPoint(ipAddr, Convert.ToInt32(mainPage.ConnectionPort));
			Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			listener.Bind(localEndPoint);
			listener.Listen(10);
			while (true)
			{
				customDebugger.LogThis("Waiting connection ... ");
				Socket clientSocket = listener.Accept();
				byte[] bytes = new Byte[1024];
				string data = null;
				while (true)
				{

					int numByte = clientSocket.Receive(bytes);
					data += Encoding.ASCII.GetString(bytes, 0, numByte);
					if (data.IndexOf("<EOF>") > -1)
						break;
				}
				var str2 = ("Text received -> " + data);
				customDebugger.LogThis(str2);
				byte[] message = Encoding.ASCII.GetBytes("Test Server");
				clientSocket.Send(message);
				clientSocket.Shutdown(SocketShutdown.Both);
				clientSocket.Close();
			}
		}
		catch (Exception ex)
		{
			customDebugger.LogThis(ex.ToString());
		}
		finally
		{
			mainPage.Listening = false;
		}
	}
}

public class DeskJoiner
{
	public CustomDebugger customDebugger { get; set; }

	public MainPage mainPage { get; set; }

	public DeskJoiner(MainPage mainPage, CustomDebugger customDebugger)
	{
		this.mainPage = mainPage;
		this.customDebugger = customDebugger;
	}

	public void DoWork()
	{
		ConnectServer();
	}

	public async void ConnectServer()
	{
		try
		{
			mainPage.Connecting = true;

			IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress ipAddr = ipHost.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);
			Socket sender = new Socket(ipAddr.AddressFamily,SocketType.Stream, ProtocolType.Tcp);

			try
			{
				sender.Connect(localEndPoint);
				customDebugger.LogThis("Socket connected to -> " +sender.RemoteEndPoint.ToString());
				byte[] messageSent = Encoding.ASCII.GetBytes("Test Client<EOF>");
				int byteSent = sender.Send(messageSent);
				byte[] messageReceived = new byte[1024];
				int byteRecv = sender.Receive(messageReceived);
				customDebugger.LogThis("Message from Server -> " +
					  Encoding.ASCII.GetString(messageReceived, 0, byteRecv));
				sender.Shutdown(SocketShutdown.Both);
				sender.Close();
			}

			catch (ArgumentNullException ane)
			{
				customDebugger.LogThis("ArgumentNullException :" + ane.ToString());
			}

			catch (SocketException se)
			{
				customDebugger.LogThis("SocketException : " + se.ToString());
			}

			catch (Exception e)
			{
				customDebugger.LogThis("Unexpected exception : " + e.ToString());
			}
		}

		catch (Exception e)
		{
			customDebugger.LogThis(e.ToString());
		}
		finally
		{
			mainPage.Connecting = false;
		}
	}

}

public class CustomDebugger
{
	public MainPage mainPage { get; set; }

	public IDispatcher dispatcher { get; set; }

	public CustomDebugger(MainPage mainPage, IDispatcher dispatcher)
	{
		this.mainPage = mainPage;
		this.dispatcher = dispatcher;
	}

	public void LogThis(string log)
	{
		string customDate = DateTime.Now.ToString("[HH.mm.ss.ff]");
		dispatcher.Dispatch((Action)(() => mainPage.AddToList(customDate + log)));
		Debug.WriteLine(customDate + log);
	}
}

public partial class MainPage : ContentPage
{
	public IDispatcher dispatcher { get; set; }

	public Thread listenerThread { get; set; }

	public Thread joinerThread { get; set; }

	public DeskListener deskListener { get; set; }

	public DeskJoiner deskJoiner { get; set; }

	public CustomDebugger customDebugger { get; set; }

	public ObservableCollection<string> DebugLogList { get; set; }

	public string IpAdress_ { get; set; }
	public string IpAdress { get { return IpAdress_; } set { IpAdress_ = value; OnPropertyChanged(nameof(IpAdress)); Preferences.Set("ip", IpAdress_); } }

	public string ConnectionPort_ { get; set; }
	public string ConnectionPort { get { return ConnectionPort_; } set { ConnectionPort_ = value; OnPropertyChanged(nameof(ConnectionPort)); Preferences.Set("port", ConnectionPort_); } }

	public bool Listening_ { get; set; }
	public bool Listening { get { return Listening_; } set { Listening_ = value; OnPropertyChanged(nameof(Listening)); } }

	public bool Connecting_ { get; set; }
	public bool Connecting { get { return Connecting_; } set { Connecting_ = value; OnPropertyChanged(nameof(Connecting)); } }

	public bool Sending_ { get; set; }
	public bool Sending { get { return Sending_; } set { Sending_ = value; OnPropertyChanged(nameof(Sending)); } }

	public MainPage()
	{
		InitializeComponent();
		this.BindingContext = this;
		dispatcher = Application.Current.Dispatcher;
		customDebugger = new CustomDebugger(this, dispatcher);
		deskListener = new DeskListener(this, customDebugger);
		deskJoiner = new DeskJoiner(this, customDebugger);
		DebugLogList = new ObservableCollection<string>();

		IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
		IPAddress ipAddr = ipHost.AddressList[2];
		IpAdress = Preferences.Get("ip", ipAddr.ToString());
		ConnectionPort = Preferences.Get("port", "81");
	}

	public void AddToList(string item)
	{
		DebugLogList.Add(item);
		DebugLogList.Move(DebugLogList.Count -1 , 0);
		OnPropertyChanged(nameof(DebugLogList));
	}

	private void button_listen_Clicked(object sender, EventArgs e)
	{
		listenerThread = new Thread(deskListener.DoWork);
		listenerThread.Start();
		customDebugger.LogThis("Thread started Id:" + listenerThread.ManagedThreadId);
	}

	private void button_connect_Clicked(object sender, EventArgs e)
	{
		joinerThread = new Thread(deskJoiner.DoWork);
		joinerThread.Start();
		customDebugger.LogThis("Thread started Id:" + joinerThread.ManagedThreadId);
	}

	private void button_send_Clicked(object sender, EventArgs e)
	{

	}
}

