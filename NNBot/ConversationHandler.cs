using System;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace NNBot
{
	public class ConversationHandler
	{
		private object lck = new object();
		private DateTime lastHeard;
		private DateTime lastTalked;
		private UUID historyid;
		private Bot.Reply talk;
		double debt = 50;
		public ConversationHandler (UUID id, Bot.Reply handler)
		{
			talk = handler;
			historyid = id;

		}

		public void start()
		{
			lastTalked =  DateTime.Now;
			lastHeard = lastTalked;
			Task.Run(() => {
				while (true) {
					Thread.Sleep((int)(Convert.ToDouble(Bot.configuration["talkinterval"])*1000));
					tick();
				}
			});
		}

		public void incomingMessage(string message) {
			lock (lck) {
				lastHeard = DateTime.Now;
				debt+= message.Length;
			}
		}

		private void tick() {
			DateTime now = DateTime.Now;
			double timeHeard = (now - lastHeard).TotalMinutes;
			double timeTalked = (now - lastTalked).TotalMinutes;
			double talkProb = 0.05;
			//if (timeHeard < 2) talkProb *= Math.Exp ((timeHeard - 2) * 3);
			//talkProb *= Math.Exp ((timeTalked - 5) / 2);
			lock (lck) {
				if (debt > 0 && timeTalked > 0)
					debt *= Convert.ToDouble(Bot.configuration["talkdecay"]);
				debt -= Convert.ToDouble(Bot.configuration["talkdecrement"]);
				talkProb *= Math.Exp (-debt / 20);
			}

			Console.WriteLine("timeHeard=" + timeHeard + " timeTalked=" + timeTalked + " debt=" + debt + " prob=" + talkProb);
			if (Bot.rand.NextDouble() < talkProb) {
				string message = NNInterface.getLine (ConversationHistory.getHistory (historyid).get ());
				if (message != "")
					talk (message);
				debt += 5 + message.Length;
				lastTalked = DateTime.Now;
			}
		}
	}
}

