using System;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace NNBot
{
	public class ConversationHandler
	{
		private DateTime lastHeard;
		private DateTime lastTalked;
		private UUID historyid;
		Bot.Reply talk;
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
					Thread.Sleep(5000);
					tick();
				}
			});
		}

		public void incomingMessage() {
			lastHeard = DateTime.Now;
		}

		private void tick() {
			DateTime now = DateTime.Now;
			double timeHeard = (now - lastHeard).TotalMinutes;
			double timeTalked = (now - lastTalked).TotalMinutes;
			double talkProb = 0.2;
			if (timeHeard < 2) talkProb *= Math.Exp ((timeHeard - 2) * 3);
			talkProb *= Math.Exp ((timeTalked - 5) / 2);
			Console.WriteLine("timeHeard=" + timeHeard + " timeTalked=" + timeTalked + " prob=" + talkProb);
			if (Bot.rand.NextDouble() < talkProb) {
				talk (NNInterface.getLine (ConversationHistory.getHistory (historyid).get ()));
				lastTalked = DateTime.Now;
			}
		}
	}
}

