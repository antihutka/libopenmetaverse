using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace NNBot
{
	public class ConversationHistory
	{
		private readonly object lck = new object();
		private int length = 0;
		private int maxlength = 0;
		private List<string> history = new List<string>();

		public ConversationHistory (int ml)
		{
			maxlength = ml;
		}
		public void add(string message)
		{
			lock (lck) {
				length += message.Length;
				history.Add (message);
				if (length > (maxlength - history[0].Length)) {
					length -= history [0].Length;
					history.RemoveAt (0);
				}
			}
		}
		public string get()
		{
			lock (lck) {
				return String.Join ("\n", history) + "\n"; 
			}
		}

		public static Dictionary<UUID, ConversationHistory> conversations = new Dictionary<UUID, ConversationHistory>();
		private static object staticlck = new object();
		public static ConversationHistory getHistory(UUID id) {
			lock (staticlck) {
				if (!conversations.ContainsKey (id))
					conversations[id] = new ConversationHistory(2048);
				return conversations [id];
			}
		}
		internal static void dump(Bot.Reply reply) {
			lock(staticlck) {
				foreach(var hist in conversations) {
					reply (hist.Key + "\n" + hist.Value.get());
				}
			}
		}
	}
}
