using System;
using OpenMetaverse;
using System.Collections.Concurrent;

namespace NNBot
{
	public class Groups
	{
		public Groups()
		{
		}
		public static bool isInGroup(UUID usr, UUID gr)
		{
			var q = new BlockingCollection<bool>();
			EventHandler<GroupMembersReplyEventArgs> handler = null;
			handler = (sender, e) => {
				q.Add(e.Members.ContainsKey(usr));
			};
			Bot.Client.Groups.GroupMembersReply += handler;
			Bot.Client.Groups.RequestGroupMembers(gr);
			bool result = false;
			q.TryTake(out result, 15 * 1000);
			Bot.Client.Groups.GroupMembersReply -= handler;
			return false;
		}
	}
}
