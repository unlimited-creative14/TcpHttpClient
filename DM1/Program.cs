using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;

namespace DM1
{
	class Program
	{

		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			var activeInterfaces = interfaces.Where((x) => { return x.OperationalStatus == OperationalStatus.Up; });

			//var wifi = activeInterfaces.Where(x => x.Name.ToUpper() == "ETHERNET").FirstOrDefault();
			var eth = activeInterfaces.Where(x => x.Name.ToUpper() == "ETHERNET").FirstOrDefault();

			//TcpClient wifiClient = new TcpClient(
			//	new IPEndPoint(wifi.GetIPProperties().UnicastAddresses[0].Address, 
			//	0
			//));
			TcpClient ethClient = new TcpClient(
				new IPEndPoint(
					eth.GetIPProperties().UnicastAddresses[0].Address,
					0
			));

			var cli = new MyHttpClient(ethClient);
			MyHttpClient.Response response;
			cli.Connect("aws.random.cat", 443);
			if (cli.SendRequest(new MyHttpClient.Request("aws.random.cat", "/meow") { 
				Method = "GET",
				Body = ""
			}))
			{
				response = cli.RecvResponse();
				Console.WriteLine(response.Body);
			}
		}
	}
}
