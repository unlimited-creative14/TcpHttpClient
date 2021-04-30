using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace DM1
{
	class ChunkedStream : Stream
	{
		// Reuse code from: https://github.com/frohoff/jdk8u-jdk/blob/master/src/share/classes/sun/net/www/http/ChunkedInputStream.java
		Stream stream;
		const string crlf = "\r\n";
		public ChunkedStream(Stream responseStream, Response response = null)
		{
			stream = responseStream;
			State = ChunkState.AwaitingChunkHeader;
			this.response = response;
		}

		public enum ChunkState
		{
			/**
			* <summary>State to indicate that next field should be :-
			*  chunk-size [ chunk-extension ] CRLF</summary>
			*/
			AwaitingChunkHeader = 1,

			/**
			 * <summary>State to indicate that we are currently reading the chunk-data.</summary>
			 */
			ReadingChunk,

			/**
			* <summary>Indicates that a chunk has been completely read and the next
			* fields to be examine should be CRLF</summary>
			*/
			AwaitingChunkEnd,

			/**
			* <summary>Indicates that all chunks have been read and the next field
			* should be optional trailers or an indication that the chunked
			* stream is complete.</summary>
			*/
			AwaitingChunkTrailer,

			/**
			* <summary>State to indicate that the chunked stream is complete and
			* no further bytes should be read from the underlying stream.</summary>
			*/
			Done
		}

		public ChunkState State { get; private set; }

		public override bool CanRead => stream.CanRead;

		public override bool CanSeek => stream.CanSeek;

		public override bool CanWrite => stream.CanWrite;

		public override long Length => stream.Length;

		public override long Position { get => throw new NotImplementedException(); set => stream.Position = value; }

		public override void Flush()
		{
			throw new NotSupportedException();
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

		bool IsHexDigit(char x)
		{
			var s = "0123456789ABCDEF";

			return s.Contains(x);
		}

		const int internalBufferSize = 4096;
		int rawPos, rawCount;
		int chunkSize, chunkRead, chunkPos, chunkCount;
		byte[] chunkData = new byte[internalBufferSize];
		byte[] rawData = new byte[internalBufferSize];

		Response response;

		// follow Java ChunkedInputStream
		public static readonly int MaxChunkHeaderSize = 2048 + crlf.Length;

		private void EnsureRawAvailable(int size)
		{
			if (rawCount + size > rawData.Length)
			{
				int used = rawCount - rawPos;
				if (used + size > rawData.Length)
				{
					byte[] tmp = new byte[used + size];
					if (used > 0)
					{
						Array.Copy(rawData, rawPos, tmp, 0, used);
					}
					rawData = tmp;
				}
				else
				{
					if (used > 0)
					{
						Array.Copy(rawData, rawPos, rawData, 0, used);
					}
				}
				rawCount = used;
				rawPos = 0;
			}
		}

		private int FastRead(byte[] b, int off, int len)
		{

			// assert state == STATE_READING_CHUNKS;

			int remaining = chunkSize - chunkRead;
			int cnt = (remaining < len) ? remaining : len;
			if (cnt > 0) {
				int nread;
				try {
					nread = stream.Read(b, off, cnt);
				} catch (IOException e) {
					throw e;
				}
				if (nread > 0)
				{
					chunkRead += nread;
					if (chunkRead >= chunkSize)
					{
						State = ChunkState.AwaitingChunkEnd;
					}
					return nread;
				}
				throw new IOException("Premature EOF");
			} else
			{
				return 0;
			}
		}

		public void ProcessRaw()
		{
			int pos;
			while (State != ChunkState.Done)
			{
				switch (State)
				{
					case ChunkState.AwaitingChunkHeader:
						pos = rawPos;
						while (pos < rawCount)
						{
							if (rawData[pos] == '\n')
							{
								break;
							}
							pos++;
							if ((pos - rawPos) >= MaxChunkHeaderSize)
							{
								throw new IOException("Chunk header too long");
							}
						}
						if (pos >= rawCount)
						{
							return;
						}

						string hexLength = Encoding.ASCII.GetString(rawData, rawPos, pos - rawPos + 1);
						chunkSize = int.Parse(hexLength, System.Globalization.NumberStyles.HexNumber);

						// move pointer to data section
						rawPos = pos + 1;
						chunkRead = 0;

						if (chunkSize > 0)
						{
							State = ChunkState.ReadingChunk;
						}
						else
						{
							State = ChunkState.AwaitingChunkTrailer;
						}
						break;

					case ChunkState.ReadingChunk:

						if (rawPos >= rawCount)
						{
							return;
						}
						int copyLen = Math.Min(chunkSize - chunkRead, rawCount - rawPos);

						if (chunkData.Length < chunkCount + copyLen)
						{
							int cnt = chunkCount - chunkPos;
							if (chunkData.Length < cnt + copyLen)
							{
								byte[] tmp = new byte[cnt + copyLen];
								Array.Copy(chunkData, chunkPos, tmp, 0, cnt);
								chunkData = tmp;
							}
							else
							{
								Array.Copy(chunkData, chunkPos, chunkData, 0, cnt);
							}
							chunkPos = 0;
							chunkCount = cnt;
						}
						Array.Copy(rawData, rawPos, chunkData, chunkCount, copyLen);
						rawPos += copyLen;
						chunkCount += copyLen;
						chunkRead += copyLen;

						if (chunkSize - chunkRead <= 0)
						{
							State = ChunkState.AwaitingChunkEnd;
						}
						else
						{
							return;
						}
						break;
					case ChunkState.AwaitingChunkEnd:
						if (rawPos + 1 >= rawCount)
						{
							return;
						}

						if (rawData[rawPos] != '\r')
						{
							throw new IOException("missing CR");
						}
						if (rawData[rawPos + 1] != '\n')
						{
							throw new IOException("missing LF");
						}
						rawPos += 2;

						/*
						 * Move onto the next chunk
						 */
						State = ChunkState.AwaitingChunkHeader;
						break;
					case ChunkState.AwaitingChunkTrailer:
						pos = rawPos;
						while (pos < rawCount)
						{
							if (rawData[pos] == '\n')
							{
								break;
							}
							pos++;
						}
						if (pos >= rawCount)
						{
							return;
						}

						if (pos == rawPos)
						{
							throw new IOException("LF should be proceeded by CR");
						}
						if (rawData[pos - 1] != '\r')
						{
							throw new IOException("LF should be proceeded by CR");
						}
						if (pos == (rawPos + 1))
						{
							State = ChunkState.Done;
							return;
						}

						string trailer = Encoding.ASCII.GetString(rawData, rawPos, pos - rawPos);

						var kv = trailer.Split(':', 2);
						if (kv.Length !=2)
							throw new IOException("Malformed tailer - format should be key:value");

						response.Headers.Add(kv[0], kv[1]);


						rawPos = pos + 1;
						break;

					case ChunkState.Done:
						break;
					default:
						break;
				}
			}
		}

		private int ReadAheadNonBlocking()
		{

			/*
			 * If there's anything available on the underlying stream then we read
			 * it into the raw buffer and process it. Processing ensures that any
			 * available chunk data is made available in chunkData.
			 */
			int avail = internalBufferSize;
			if (avail > 0) {

				/* ensure that there is space in rawData to read the available */
				EnsureRawAvailable(avail);

				int nread;
				try {
					nread = stream.Read(rawData, rawCount, avail);
				} catch (IOException e) {
					throw e;
				}
				if (nread < 0)
				{
				   /* premature EOF ? */
					return 0;
				}
				rawCount += nread;

				/*
					* Process the raw bytes that have been read.
					*/
				ProcessRaw();
			}

        /*
         * Return the number of chunked bytes available to read
         */
			return chunkCount - chunkPos;
		}

		private int ReadAheadBlocking()
		{
			do
			{
				/*
				 * All of chunked response has been read to return EOF.
				 */
				if (State == ChunkState.Done)
				{
					return 0;
				}

				/*
				 * We must read into the raw buffer so make sure there is space
				 * available. We use a size of 32 to avoid too much chunk data
				 * being read into the raw buffer.
				 */
				//EnsureRawAvailable(32);
				EnsureRawAvailable(internalBufferSize);
				int nread;
				try
				{
					nread = stream.Read(rawData, rawCount, rawData.Length - rawCount);
				}
				catch (IOException e)
				{
					throw e;
				}

				/**
				 * If we hit EOF it means there's a problem as we should never
				 * attempt to read once the last chunk and trailers have been
				 * received.
				 */
				if (nread < 0)
				{
					throw new IOException("Premature EOF");
				}

				/**
				 * Process the bytes from the underlying stream
				 */
				rawCount += nread;
				ProcessRaw();

			} while (chunkCount <= 0);

			/*
			 * Return the number of chunked bytes available to read
			 */
			return chunkCount - chunkPos;
		}

		private int ReadAhead(bool allowBlocking)
		{
			if (State == ChunkState.Done)
			{
				return 0;
			}

			/*
			 * Reset position/count if data in chunkData is exhausted.
			 */
			if (chunkPos >= chunkCount)
			{
				chunkCount = 0;
				chunkPos = 0;
			}

			/*
			 * Read ahead blocking or non-blocking
			 */
			if (allowBlocking)
			{
				return ReadAheadBlocking();
			}
			else
			{
				return ReadAheadNonBlocking();
			}
		}

		public override int Read(byte[] b, int off, int len)
		{
			if ((off < 0) || (off > b.Length) || (len < 0) ||
				((off + len) > b.Length) || ((off + len) < 0))
			{
				throw new Exception("Out of Range");
			}
			else if (len == 0)
			{
				return 0;
			}

			int avail = chunkCount - chunkPos;
			if (avail <= 0)
			{
				/*
				 * Optimization: if we're in the middle of the chunk read
				 * directly from the underlying stream into the caller's
				 * buffer
				 */
				if (State == ChunkState.ReadingChunk)
				{
					return FastRead(b, off, len);
				}

				/*
				 * We're not in the middle of a chunk so we must read ahead
				 * until there is some chunk data available.
				 */
				avail = ReadAhead(true);
				if (avail < 0)
				{
					return 0;      /* EOF */
				}
			}
			int cnt = (avail < len) ? avail : len;
			Array.Copy(chunkData, chunkPos, b, off, cnt);
			chunkPos += cnt;

			return cnt;
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
			throw new NotSupportedException();
		}
	}
}
