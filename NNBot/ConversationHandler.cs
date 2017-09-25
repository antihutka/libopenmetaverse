﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
		private double othertalk = 1, selftalk = 100, boost = 0;
		private bool thinking = false;
		private int quiet = 0;
		string kw_last;
		string[] kw_split;
		StreamWriter logfile;

		public ConversationHandler(string key, Bot.Reply handler)
		{
			logfile = new StreamWriter("talkinfo." + Bot.configuration["firstname"] + "." + Bot.configuration["lastname"] + ".log");
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

		public void setquiet(int q)
		{
			lock(lck)
			{
				quiet = q;
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
				if (quiet > 0) quiet--;
				double targetratio = Convert.ToDouble(Bot.configuration["targetratio"]);
				double talkadd = Convert.ToDouble(Bot.configuration["talkadd"]);
				double respboost = 0;
				if (timeHeard < timeTalked) respboost = Convert.ToDouble(Bot.configuration["respboost"]);
				double talkratio = (talkadd*targetratio + selftalk) / (talkadd + othertalk + boost + respboost);
				talkratio /= targetratio;
				talkProb /= Math.Pow(talkratio, 8) + 0.00001;
				double talkthr = Convert.ToDouble(Bot.configuration["talkthr"]);
				double talkthrdiv = Convert.ToDouble(Bot.configuration["talkthrdiv"]);
				double talkthrottle = selftalk / targetratio + othertalk - talkthr + quiet * 10;
				if (talkthrottle > 0) talkProb /= Math.Exp((talkthrottle)/(talkthrdiv));
				if (talkProb > 1) talkProb = 1;
				if (thinking) talkProb = 0;
				string message = "tHear=" + timeHeard.ToString("n2") + " tTalk=" + timeTalked.ToString("n2") + " boost=" + boost.ToString("n0") +
				                                     " quiet=" + quiet.ToString() + " oTalk=" + othertalk.ToString("n4") + " sTalk=" + selftalk.ToString("n4") +
				                                     " ratio=" + talkratio.ToString("n4") + " prob=" + (talkProb*100).ToString("n2") + "%";
				if (Convert.ToInt32(Bot.configuration["talkinfo"]) > 0)
					                                     Console.WriteLine(message);
				Console.Title = message;
				logfile.WriteLine(message); logfile.Flush();
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
