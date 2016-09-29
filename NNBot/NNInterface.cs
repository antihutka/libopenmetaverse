using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NNBot
{
	public class NNInterface
	{
		private static Semaphore sem = new Semaphore(4, 4);

		public NNInterface ()
		{
		}
		public static string getLine(string context)
		{
			try {
				sem.WaitOne();
				DateTime start = DateTime.Now;

				Socket con = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				con.ReceiveTimeout = 600 * 1000;
				//Console.WriteLine("Socket created");
				con.Connect(Bot.configuration["nnhost"], Convert.ToInt32(Bot.configuration["nnport"]));
				//Console.WriteLine("Connected");
				byte[] econtext = Encoding.UTF8.GetBytes(context);
				con.Send(econtext);
				//Console.WriteLine("Sent");
				con.Shutdown(SocketShutdown.Send);
				//Console.WriteLine("Shutdown");
				string reply = readAll(con);

				TimeSpan elap = DateTime.Now - start;
				Console.WriteLine("Context length = " + econtext.Length + " time = " + elap.TotalSeconds);

				return reply.Trim();
			} catch (SocketException e) {
				Console.WriteLine("Can't get NN reply: " + e.ToString());
				return "";
			} finally {
				sem.Release ();
			}
		}

		private static string readAll(Socket s)
		{
			byte[] buf = new byte[2048];
			int read = 0, ret;
			//Console.WriteLine ("Reading reply");
			while (0 != (ret = s.Receive(buf, read, buf.Length - read, SocketFlags.None))) {
				read += ret;
				//Console.WriteLine ("Read " + ret + " bytes: " + Encoding.UTF8.GetString(buf, 0, read));
			}
			//Console.WriteLine ("Reading done");
			return Encoding.UTF8.GetString(buf, 0, read);
		}
	}
}

