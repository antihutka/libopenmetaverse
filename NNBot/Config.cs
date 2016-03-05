using System;
using System.Collections.Generic;
using System.IO;

namespace NNBot
{
	public class Config
	{
		
		public Config ()
		{
		}

		public static Dictionary<String, String> LoadConfig(string file)
		{
			var dic = new Dictionary<String, String>();
			var data = File.ReadAllLines (file);
			foreach (string l in data) {
				var i = l.IndexOf ("=");
				if (i >= 0) {
					var k = l.Substring (0, i).Trim();
					var v = l.Substring (i + 1).Trim();
					dic.Add (k, v);
				}
			}
			return dic;
		}


	}
}

