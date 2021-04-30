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
			Console.WriteLine("Hello World!");

			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			var activeInterfaces = interfaces.Where((x) => { return x.OperationalStatus == OperationalStatus.Up; });

			var wifi = activeInterfaces.Where(x => x.Name.ToUpper() == "WIFI").FirstOrDefault();
			var eth = activeInterfaces.Where(x => x.Name.ToUpper() == "ETHERNET").FirstOrDefault();

			var x1 = new Uri("https://www.learningcontainer.com/wp-content/uploads/2020/05/sample-large-zip-file.zip");
			var x2 = new Uri("https://api.ipify.org");

			Request request = Request.DefaultRequest("GET", x1);
			request.Headers.Add("Upgrade-Insecure-Requests", "1");

			HttpClientBasedOnTCPClient client = new HttpClientBasedOnTCPClient(eth);

			Console.WriteLine($"{request}");
			File.Delete("./mmx.zip");
			var fs = File.OpenWrite("./mmx.zip");

			//var resp = client.Send(request);
			var task = client.DownloadFileAsync(x1, fs);
			task.Wait();
		}
	}
}
