using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

namespace DM1
{
	public class Response
	{
		const string crlf = "\r\n";
		const string space = " ";

		object? ValueOrDefault(IDictionary<string, string> dict, string key, object? defaultVal = null)
		{
			if (dict.TryGetValue(key, out string s))
			{
				return s;
			}
			return defaultVal;
		}

		// Get the request of this response
		public Request Request { get; private set; }
		public IDictionary<string, string> Headers;

		public Version Version { get; private set; }
		public int StatusCode { get; private set; }
		public string Status { get; private set; }

		// -----------------------Begin General header----------------------------------------
		public string CacheControl { get => ValueOrDefault(Headers, "Cache-Control") as string; }
		public string Connection { get => ValueOrDefault(Headers, "Connection") as string; }
		public DateTime Date { get => DateTime.Parse(ValueOrDefault(Headers, "Date", DateTime.Now) as string); }
		public string Pragma { get => ValueOrDefault(Headers, "Pragma") as string; }
		public string Trailer { get => ValueOrDefault(Headers, "Trailer") as string; }
		public string TransferEncoding { get => ValueOrDefault(Headers, "Transfer-Encoding") as string; }
		public string Upgrade { get => ValueOrDefault(Headers, "Upgrade") as string; }
		public string Via { get => ValueOrDefault(Headers, "Via") as string; }
		public string Warning { get => ValueOrDefault(Headers, "Warning") as string; }

		// Only for Response
		public string AcceptRanges { get => ValueOrDefault(Headers, "Accept-Ranges") as string; }
		public int Age { get => int.Parse(ValueOrDefault(Headers, "Age", "-1") as string); }
		public string ETag { get => ValueOrDefault(Headers, "ETag") as string; }
		public string Location { get => ValueOrDefault(Headers, "Location") as string; }
		public string ProxyAuthenticate { get => ValueOrDefault(Headers, "Location") as string; }
		public string Server { get => ValueOrDefault(Headers, "Server") as string; }
		public string Vary { get => ValueOrDefault(Headers, "Vary") as string; }
		public string WWWAuthenticate { get => ValueOrDefault(Headers, "WWW-Authenticate") as string; }

		public string ContentType { get => ValueOrDefault(Headers, "Content-Type") as string; }
		public string ContentEncoding { get => ValueOrDefault(Headers, "Content-Encoding") as string; }
		public int ContentLength { get => int.Parse(ValueOrDefault(Headers, "Content-Length", "-1") as string); }
		public HttpContent Content { get; private set; }

		public override string ToString()
		{
			string s = "HTTP/" + Version + space + StatusCode + space + Status + crlf;
			foreach (var header in Headers)
			{
				s += header.Key + ':' + space + header.Value + crlf;
			}

			return s + crlf;
		}



		static public Response GetResponseFromStream(Stream stream, Request request = null)
		{
			var r = new Response();
			r.Request = request;
			var header = ReadHeader(stream);
			var lines = header.Split(crlf, StringSplitOptions.RemoveEmptyEntries);

			var start_line = lines[0].Split(space);

			r.Version = new Version(start_line[0].Split('/')[1]);
			r.StatusCode = int.Parse(start_line[1]);
			r.Status = start_line[2];

			int i = 0;
			foreach (var line in lines[1..])
			{
				var sep_line = line.Split(':', 2);
				if (r.Headers.ContainsKey(sep_line[0].Trim()))
				{
					i++;
					r.Headers.Add(sep_line[0].Trim() + i.ToString(), sep_line[1].Trim());
				}
				else
					r.Headers.Add(sep_line[0].Trim(), sep_line[1].Trim());
			}

			if (r.TransferEncoding != null)
			{
				r.Content = new StreamContent(new ChunkedStream(stream));
			}
			else
			{
				r.Content = new StreamContent(stream);
			}
			return r;
		}

		static string ReadUntil(Stream stream, string stop, int count = 0)
		{
			string s = "";

			while (stream.CanRead)
			{
				s += (char)stream.ReadByte();
				if (s.Length > stop.Length && s[^stop.Length..] == stop)
				{
					if (count == 0)
						return s[..(s.Length - stop.Length)];
					count--;
				}
			}

			return "";
		}

		static string ReadHeader(Stream stream)
		{
			string header = ReadUntil(stream, crlf + crlf) + crlf + crlf;
			return header;
		}

		Response()
		{
			Headers = new Dictionary<string, string>();
			
		}
	}
}
