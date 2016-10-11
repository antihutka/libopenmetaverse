using System;
using System.Threading;
using System.Threading.Tasks;

namespace NNBot
{
	public class ConversationHandler
	{
		private readonly object lck = new object();
		private DateTime lastHeard;
		private DateTime lastTalked;
		readonly string nnkey;
		private readonly Bot.Reply talk;
		private double othertalk = 100, selftalk = 100;
		private bool thinking = false;

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
				othertalk += message.Length;
			}
		}

		private void tick()
		{
			DateTime now = DateTime.Now;
			double timeHeard = (now - lastHeard).TotalMinutes;
			double timeTalked = (now - lastTalked).TotalMinutes;
			double talkProb = 0.01;
			lock (lck)
			{
				selftalk *= 0.98;
				othertalk *= 0.98;
				double talkratio = (0 + selftalk) / (1 + othertalk);
				//talkProb /= Math.Exp(5*talkratio);
				talkProb /= Math.Pow(talkratio, 3) + 0.01;
				if (othertalk > 750) talkProb /= Math.Exp((othertalk - 750)/100);
				//if (timeTalked < 1) talkProb /= Math.Exp(6 - 6*timeTalked);
				if (thinking) talkProb = 0;
				Console.WriteLine("tHear=" + timeHeard.ToString("n4") + " tTalk=" + timeTalked.ToString("n4") +
								  " oTalk=" + othertalk.ToString("n4") + " sTalk=" + selftalk.ToString("n4") +
				                  " ratio=" + talkratio.ToString("n4") + " prob=" + talkProb.ToString("n4"));
			}

			if (Bot.rand.NextDouble() < talkProb)
			{
				lock(lck) thinking = true;
				NNInterfaceNew.getInterface(nnkey).getLine((s) =>
				{
					lock (lck)
					{
						selftalk += s.Length;
						thinking = false;
					}
					if (s != "")
						talk(s);
				});
				lastTalked = DateTime.Now;
			}
		}
	}
}
