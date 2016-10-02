using System;
using System.Threading;
using System.Threading.Tasks;

namespace NNBot
{
	public class ConversationHandler
	{
		private object lck = new object();
		private DateTime lastHeard;
		private DateTime lastTalked;
		readonly string nnkey;
		private readonly Bot.Reply talk;
		double debt = 50;
		private double othertalk = 100, selftalk = 25;

		public ConversationHandler(string key, Bot.Reply handler)
		{
			talk = handler;
			nnkey = key;
		}

		public void start()
		{
			lastTalked = DateTime.Now;
			lastHeard = lastTalked;
			Task.Run(() =>
			{
				while (true)
				{
					Thread.Sleep((int)(Convert.ToDouble(Bot.configuration["talkinterval"]) * 1000));
					tick();
				}
			});
		}

		public void incomingMessage(string message)
		{
			NNInterfaceNew.getInterface(nnkey).pushLine(message);
			lock (lck)
			{
				lastHeard = DateTime.Now;
				debt += message.Length;
				othertalk += message.Length;
			}
		}

		private void tick()
		{
			DateTime now = DateTime.Now;
			double timeHeard = (now - lastHeard).TotalMinutes;
			double timeTalked = (now - lastTalked).TotalMinutes;
			double talkProb = 0.033;
			//if (timeHeard < 2) talkProb *= Math.Exp ((timeHeard - 2) * 3);
			//talkProb *= Math.Exp ((timeTalked - 5) / 2);
			lock (lck)
			{
				if (debt > 0 && timeTalked > 0)
					debt *= Convert.ToDouble(Bot.configuration["talkdecay"]);
				debt -= Convert.ToDouble(Bot.configuration["talkdecrement"]);
				//talkProb *= Math.Exp (-debt / 20);
				selftalk *= 0.99;
				othertalk *= 0.99;
				double talkratio = selftalk / (1 + othertalk);
				talkProb /= Math.Exp(5*talkratio);
				if (othertalk > 2000) talkProb /= Math.Exp(othertalk - 1000);
				if (timeTalked < 1) talkProb /= Math.Exp(6 - 6*timeTalked);
				Console.WriteLine("tHear=" + timeHeard.ToString("n4") + " tTalk=" + timeTalked.ToString("n4") +
								  " oTalk=" + othertalk.ToString("n4") + " sTalk=" + selftalk.ToString("n4") +
				                  " ratio=" + talkratio.ToString("n4") + " prob=" + talkProb.ToString("n4"));
			}

			if (Bot.rand.NextDouble() < talkProb)
			{
				/*				string message = NNInterface.getLine (ConversationHistory.getHistory (historyid).get ());
								if (message != "")
									talk (message);*/
				NNInterfaceNew.getInterface(nnkey).getLine((s) =>
				{
					lock (lck) debt += s.Length;
					lock (lck) selftalk += s.Length;
					if (s != "")
						talk(s);
				});
				lock (lck) debt += 5;
				lastTalked = DateTime.Now;
			}
		}
	}
}
