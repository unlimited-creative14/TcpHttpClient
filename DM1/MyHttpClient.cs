using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace DM1
{
	class MyHttpClient
	{

		// TCP client that use to send the Request
		TcpClient tcpClient;
		Stream stream;
		const string crlf = "\r\n";

		bool secure;

		public class ChunkedStream : Stream
		{
			Stream stream;
			long pos;
			public ChunkedStream(Stream responseStream)
			{
				stream = responseStream;
			}

			public override bool CanRead => stream.CanRead;

			public override bool CanSeek => stream.CanSeek;

			public override bool CanWrite => stream.CanWrite;

			public override long Length => stream.Length;

			public override long Position { get => pos; set => throw new NotSupportedException(); }

			public override void Flush()
			{
				throw new NotSupportedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				string chunk = "";
				string hexLength = "";
				string nb;

				int length;

				do
				{
					byte[] ch = new byte[1];
					ch[0] = (byte)stream.ReadByte();
					nb = Encoding.UTF8.GetString(ch);
					chunk += nb;
					if (char.IsLetterOrDigit(nb[0]))
						hexLength += nb;
				} while (char.IsLetterOrDigit(nb[0]));
				stream.ReadByte(); // remove \n

				length = int.Parse(hexLength, System.Globalization.NumberStyles.HexNumber);

				if (length == 0)
				{
					return 0;
				}

				stream.Read(buffer, 0, length);
				stream.ReadByte();
				stream.ReadByte();

				return length;
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				return stream.Seek(offset, origin);
			}

			public override void SetLength(long value)
			{
				stream.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				stream.Write(buffer, offset, count);
			}
		}
		public class Response
		{
			public class Header
			{
				public string HttpVersion;
				public int StatusCode;
				public string Status;

				public string Connection;
				public string ContentType;
				public DateTime Date;
				public int ContentLength;
				public string[] ContentEncoding;
				public string TransferEncoding;

				public string Server;

				public Dictionary<string, string> OtherHeaders;
				public Header()
				{
					OtherHeaders = new Dictionary<string, string>();
				}

				public static Header ParseHeader(string header)
				{
					const string el = "\r\n";

					Header rp = new Header();
					var y = header.Split(el);

					var z = y[0].Split(' ');

					rp.HttpVersion = z[0];
					rp.StatusCode = int.Parse(z[1]);
					rp.Status = z[2];

					for (int i = 1; i < y.Length; i++)
					{
						var str = y[i];
						var kp = str.Split(':', 2);

						switch (kp[0])
						{
							case "Connection":
								rp.Connection = kp[1];
								break;
							case "Content-Type":
								rp.ContentType = kp[1];
								break;
							case "Date":
								DateTime.TryParse(kp[1], out rp.Date);
								break;
							case "Content-Length":
								rp.ContentLength = int.Parse(kp[1]);
								break;
							case "Server":
								rp.Server = kp[1].Trim();
								break;
							case "Transfer-Encoding":
								rp.TransferEncoding = kp[1].Trim();
								break;
							case "Content-Encoding":
								rp.ContentEncoding = kp[1]?.Trim().Split(',');
								break;
							default:
								rp.OtherHeaders[kp[0]] = kp?[1];
								break;
						}
					}

					return rp;
				}

			}

			public Header ServerHeader;
			public string Body;

			public static Response ParseServerResponse(string response)
			{
				const string el = "\r\n";
				var x = response.Split(el + el, 2);

				Response rp = new Response();
				
				rp.Body = x[1];

				var header = x[0];

				rp.ServerHeader = Header.ParseHeader(header);
				return rp;
			}

			public static Response ParseServerResponse(Header header, string body)
			{
				Response r = new Response();
				r.ServerHeader = header;
				r.Body = body;

				return r;
			}

			Response()
			{

			}

			public override string ToString()
			{
				return Body;
			}
		}
		public class Request
		{
			public string Resource, Host;

			
			const string space = " ";
			public string Method, HttpVersion = "HTTP/1.1";
			public string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8", 
						AcceptLanguage = "en-US,en;q=0.5", 
						AcceptEncoding = "",
						UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:88.0) Gecko/20100101 Firefox/88.0";
			public string Body;

			public Dictionary<string, string> OtherHeaders;
			public override string ToString()
			{
				string otherHeaders = "";
				string contentLength = Body.Length == 0 ? "" : ("Content-Length: " + Body.Length.ToString() + crlf);
				foreach (var key in OtherHeaders)
				{
					otherHeaders += key.Key + ": " + key.Value + crlf;
				}
				return Method + space + Resource + space + HttpVersion + crlf
					+ "Host: " + Host + crlf
					+ "Accept: " + Accept + crlf
					+ "Accept-Language: " + AcceptLanguage + crlf
					+ "Accept-Encoding: " + AcceptEncoding + crlf
					+ "User-Agent: " + UA + crlf
					+ contentLength
					+ otherHeaders + crlf
					+ Body;
			}


			// Resource is the part appear after hostname
			// Eg. google.com/search 
			// /search is resource of url
			public Request(string hostName, string resource)
			{
				Host = hostName;
				Resource = resource;
				OtherHeaders = new Dictionary<string, string>();
			}
			public Request(Uri uri)
			{
				Host = uri.Host;
				Resource = uri.AbsolutePath;
				OtherHeaders = new Dictionary<string, string>();
			}
		}
		
		public MyHttpClient(Func<TcpClient> genClient)
		{
			tcpClient = genClient();
		}

		public MyHttpClient(TcpClient client)
		{
			tcpClient = client;
		}

		public bool Connect(string hostname, int port)
		{
			tcpClient.Connect(hostname, port);
			if (port == 443){
				this.secure = true;
				
			}

			return tcpClient.Connected;
		}

		public bool SendRequest(Request request)
		{
			if (!tcpClient.Connected)
			{
				return false;
			}
			var s = request.ToString();
			var raw_request = Encoding.UTF8.GetBytes(s);

			if(secure)
			{
				stream = new SslStream(tcpClient.GetStream());
				(stream as SslStream).AuthenticateAsClient(request.Host); 
			}
			else
			{
				stream = tcpClient.GetStream();
			}

			if (stream.CanWrite)
			{
				stream.Write(raw_request, 0, raw_request.Length);
			}
			else
			{
				return false;
			}


			return true;
		}

		// altStream allow you to write output to file instead of memory
		public Response RecvResponse(Stream altStream = null, bool closeStream = false)
		{
			if (!tcpClient.Connected)
			{
				return null;
			}

			if (stream.CanRead)
			{
				int bufferSize = 4096;
				byte[] buffer = new byte[bufferSize];
				bool done = false;

				Response.Header h = null;
				string header = "";
				string body = "";
				bool headerRecv = false;
				do
				{
					if (!headerRecv)
					{
						byte[] ch = new byte[1];
						ch[0] = (byte)stream.ReadByte();
						string nb = Encoding.UTF8.GetString(ch);
						header += nb;

						if(header.Length > 4 && header.Substring(header.Length - 4) == crlf+crlf)
						{
							headerRecv = true;
							header = header.Remove(header.Length - 4);
							h = Response.Header.ParseHeader(header);
						}
					}
					else
					{
						if (h.TransferEncoding.ToLower() == "chunked")
						{
							string chunkedData = "";
							int length;
							do
							{
								string chunk = "";
								string hexLength = "";
								string nb;
								do
								{
									byte[] ch = new byte[1];
									ch[0] = (byte)stream.ReadByte();
									nb = Encoding.UTF8.GetString(ch);
									chunk += nb;
									if(char.IsLetterOrDigit(nb[0]))
										hexLength += nb;
								} while (char.IsLetterOrDigit(nb[0]));
								stream.ReadByte(); // remove \n

								length = int.Parse(hexLength, System.Globalization.NumberStyles.HexNumber);
								if(length == 0)
								{
									break;
								}
								
								//string nx = "";
								//while (nx != crlf)
								//{
								//	byte[] ch = new byte[1];
								//	ch[0] = (byte)stream.ReadByte();
								//	nb = Encoding.UTF8.GetString(ch);

								//	nx += nb;
								//	if (nx.Length > 2)
								//		nx = nx.Remove(0, 1);
								//}

								stream.Read(buffer, 0, length);
								
								chunk = Encoding.UTF8.GetString(buffer.AsSpan(0, length));
								Console.WriteLine(chunk);

								stream.ReadByte();
								stream.ReadByte();

								chunkedData += chunk;

							} while (length != 0);

							body = chunkedData; 
							done = true;
							
							// byte [';' chunkext] el data el
						}
						else
						{
							
							stream.Read(buffer, 0, h.ContentLength);

							body = Encoding.UTF8.GetString(buffer, 0, h.ContentLength);
							done = true;
						}
					}
				} while (!done);

				return Response.ParseServerResponse(h, body);
			}
			else
				return null;
		}
	}
}
