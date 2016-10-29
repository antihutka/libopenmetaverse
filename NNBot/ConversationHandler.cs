using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
		private double othertalk = 100, selftalk = 100, boost = 0;
		private bool thinking = false;
		string kw_last;
		string[] kw_split;

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

		public void incomingMessage(string message, bool fromObj)
		{
			NNInterfaceNew.getInterface(nnkey).pushLine(message);
			string kwc = Bot.configuration["keywords"];
			if (kw_last != kwc)
			{
				kw_split = kwc.Split(',');
				kw_last = kwc;
			}
			bool has_kw = kw_split.Any((s) => message.Contains(s));
			lock (lck)
			{
				lastHeard = DateTime.Now;
				othertalk += message.Length;
				if (has_kw && !fromObj) boost += Convert.ToDouble(Bot.configuration["boostamount"]);
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
				double td = Convert.ToDouble(Bot.configuration["talkdecay"]);
				selftalk *= td;
				othertalk *= td;
				double talkratio = (0 + selftalk) / (1 + othertalk + boost);
				talkratio /= Convert.ToDouble(Bot.configuration["targetratio"]);
				//talkProb /= Math.Exp(5*talkratio);
				talkProb /= Math.Pow(talkratio, 4) + 0.01;
				if (selftalk + othertalk > 1000) talkProb /= Math.Exp((selftalk + othertalk - 1000)/250);
				//if (timeTalked < 1) talkProb /= Math.Exp(6 - 6*timeTalked);
				if (thinking) talkProb = 0;
				string message = "tHear=" + timeHeard.ToString("n2") + " tTalk=" + timeTalked.ToString("n2") + " boost=" + boost.ToString("n0") +
								 " oTalk=" + othertalk.ToString("n4") + " sTalk=" + selftalk.ToString("n4") +
								 " ratio=" + talkratio.ToString("n4") + " prob=" + talkProb.ToString("n4");
				if (Convert.ToInt32(Bot.configuration["talkinfo"]) > 0)
					                                     Console.WriteLine(message);
				Console.Title = message;
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
						boost *= Convert.ToDouble(Bot.configuration["boostdecay"]);
					}
					if (s != "")
						talk(s);
				});
				lastTalked = DateTime.Now;
			}
		}
	}
}
