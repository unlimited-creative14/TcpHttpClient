using System;
using System.Net.NetworkInformation;
using System.Linq;
using System.IO;
using System.Net.Http.Headers;

namespace DM1
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine($"Use: DM1.exe <Url>");
				return;
			}
			var url = new Uri(args[0]);

			var fname = url.Segments[^1];

			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			var activeInterfaces = interfaces.Where((x) => { return x.OperationalStatus == OperationalStatus.Up;});

			int id;
			string index;
			do
			{
				for (int i = 0; i < activeInterfaces.ToArray().Length; i++)
				{
					var intf = activeInterfaces.ToArray()[i];
					Console.WriteLine($"{i}.{intf.Name} IP:{intf.GetIPProperties().UnicastAddresses[0].Address}");
				}
				Console.Write("Select a connection to begin your download: ");
				index = Console.ReadLine();
			} while (!int.TryParse(index, out id));

			var choosenInt = activeInterfaces.ToArray()[id];

			//https://speed.hetzner.de/100MB.bin

			HttpClientBasedOnTCPClient client = new HttpClientBasedOnTCPClient(choosenInt);
			File.Delete(fname);
			var fs = File.OpenWrite(fname);

			//var resp = client.Send(request);
			var task = client.DownloadFileAsync(url, fs);
			task.Wait();
		}
	}
}
