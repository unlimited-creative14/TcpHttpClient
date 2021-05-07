using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace DM1
{
	
	public class Request : HttpRequestMessage
	{
		const string crlf = "\r\n";
		const string space = " ";

		public static Request DefaultRequest(string method, Uri requestUri)
		{
			Request request = new Request(method, requestUri);
			request.Headers.UserAgent.Add(HttpClientBasedOnTCPClient.DefaultUserAgent);
			request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
			request.Headers.Connection.Add("keep-alive");

			return request;
		}

		public override string ToString()
		{
			string s = Content?.ReadAsStringAsync().Result;

			return Method.Method + space + RequestUri.AbsolutePath + space + "HTTP/" + Version + crlf
				+ Headers + Content?.Headers + crlf + s;
		}
		public Request(string method, Uri uri) : base(new HttpMethod(method), uri)
		{
			Headers.Host = uri.Host;
		}
	}

	public class HttpClientBasedOnTCPClient
	{
		// TCP client that use to send the Request
		//TcpClient tcpClient;
		NetworkInterface @interface;
		Stream stream;
		const string crlf = "\r\n";

		const string ProdName = "C#HttpClientBasedOnTCPClient";
		public static ProductInfoHeaderValue DefaultUserAgent = new ProductInfoHeaderValue(ProdName, "1.0"); 

		public bool Secure { get; private set; }


		// Bind httpClient to specific network interface
		public HttpClientBasedOnTCPClient(NetworkInterface intf)
		{
			@interface = intf;
		}

		TcpClient Connect(string hostname, int port)
		{
			TcpClient tcpClient = new TcpClient(new IPEndPoint(@interface.GetIPProperties().UnicastAddresses[0].Address.MapToIPv4(), 0));

			tcpClient.Connect(hostname, port);

			if (!tcpClient.Connected)
			{
				return null;
			}

			if (port == 443){
				Secure = true;
			}

			if (Secure)
			{
				stream = new SslStream(tcpClient.GetStream());
				(stream as SslStream).AuthenticateAsClient(hostname);
			}
			else
			{
				stream = tcpClient.GetStream();
			}

			return tcpClient;
		}

		public Response Send(Request request)
		{
			var tcpClient = Connect(request.RequestUri.Host, request.RequestUri.Port);
			if (tcpClient == null)
				throw new Exception("Not connected");

			StreamWriter streamWriter = new StreamWriter(stream);

			if (request.Headers.UserAgent.Count == 0)
				request.Headers.UserAgent.Add(DefaultUserAgent);

			streamWriter.AutoFlush = true;
			streamWriter.Write(request);

			return Response.GetResponseFromStream(stream, request);
		}

		public Response Get(Uri requestUri)
		{
			Request request = Request.DefaultRequest("GET", requestUri);

			return Send(request);
		}

		public Response Post(Uri requestUri, HttpContent content)
		{
			Request request = Request.DefaultRequest("POST", requestUri);
			
			return Send(request);
		}

		

		static void CopyStream(Stream input, Stream output, Action<int> updateProgress)
		{
			byte[] buffer = new byte[32768];
			int read;
			while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
			{
				output.Write(buffer, 0, read);
				updateProgress(read);
			}
		}
		public async Task<long> DownloadFileAsync(Uri requestUri, Stream fileOutput)
		{
			Request request = Request.DefaultRequest("GET", requestUri);
			var response = Send(request);
			long readAll = 0;
			CopyStream(await response.Content.ReadAsStreamAsync(), fileOutput, frag => { 
				readAll += frag; 
				if (response.ContentLength != -1)
				{
					Console.WriteLine($"{readAll}/{response.ContentLength} -- {100.0*readAll/response.ContentLength:0.00}%");
				}
				else
				{
					Console.WriteLine($"{readAll} bytes");
				}
			});
			return readAll;
		}
		

	}
}
