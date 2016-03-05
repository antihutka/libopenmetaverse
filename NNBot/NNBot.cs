using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace NNBot
{
	class Bot
	{
		public static GridClient Client;
		public static Dictionary<String,String> configuration;
		public static DatabaseWriter dbw;

		public static void Main(string[] args)
		{
			string configfile = "default.cfg";
			if (args.Length > 0)
				configfile = args [0];
			configuration = Config.LoadConfig (configfile);
			Console.WriteLine (string.Join(";",configuration));

			dbw = new DatabaseWriter (configuration ["dbstring"]);

			Client = new GridClient();
			Client.Settings.LOGIN_SERVER = configuration["loginserver"];
			//Client.Network.RegisterCallback(PacketType.ChatFromSimulator, ChatFromSimulatorHandler);
			Client.Self.IM += new EventHandler<InstantMessageEventArgs> (IMHandler);
			Client.Self.ChatFromSimulator += new EventHandler<ChatEventArgs> (ChatHandler);
			Client.Avatars.UUIDNameReply += new EventHandler<UUIDNameReplyEventArgs> (UUIDNameHandler);
			bool loggedIn = Client.Network.Login(configuration["firstname"], configuration["lastname"], configuration["password"], "NNBot", "NNBot 0.1");
			if (loggedIn) Console.WriteLine("Logged In");
			else Console.WriteLine("Failed");
		}

		private static void UUIDNameHandler(object sender, UUIDNameReplyEventArgs e)
		{
			foreach (UUID id in e.Names.Keys)
				NameCache.recvName (id, e.Names [id]);
		}

		private static void ChatFromSimulatorHandler(object sender, PacketReceivedEventArgs e)
		{
			string logMessage = "[local]";
			ChatFromSimulatorPacket p = (ChatFromSimulatorPacket)e.Packet;
			byte type = p.ChatData.ChatType;
			string from = Utils.BytesToString (p.ChatData.FromName);
			string message = Utils.BytesToString( p.ChatData.Message );

			if (type == 4 || type == 5) // Ignore start/stop typing messages
				return;

			logMessage += "[from " + from + "]";
			logMessage += "[type " + type + "]";
			logMessage += " " + message;
			Console.WriteLine( logMessage );
			if( message == "date" )
			{
				Client.Self.Chat( "It is now " +DateTime.Now, 0, ChatType.Normal);
			}
		}

		private static void ChatHandler(object sender, ChatEventArgs e)
		{
			bool log = true;
			switch (e.Type) {
			case ChatType.StopTyping:
			case ChatType.StartTyping:
				log = false;
				break;
			case ChatType.Normal:
			case ChatType.Whisper:
			case ChatType.Shout:
				Console.WriteLine  ("[local]["  +e.FromName  + "] "  +e.Message);
				break;
			default:
				Console.WriteLine ("Unknown chat type " + e.Type + " from " + e.FromName + ":" + e.Message);
				break;
			}

			if (log) {
				dbw.logChatEvent(e);
			}
		}

		public delegate void Reply(string s);

		private static void IMHandler(object sender, InstantMessageEventArgs e)
		{
			bool log = true;
			switch (e.IM.Dialog) {
			case InstantMessageDialog.StartTyping:
			case InstantMessageDialog.StopTyping:
				log = false;
				break;
			case InstantMessageDialog.RequestLure:
				Console.WriteLine ("Teleport request from " + e.IM.FromAgentName + " , sending offer");
				Client.Self.SendTeleportLure (e.IM.FromAgentID);
				break;
			case InstantMessageDialog.RequestTeleport:
				Console.WriteLine ("Teleport offer from " + e.IM.FromAgentName);
				if (isOwner (e.IM.FromAgentName))
					Client.Self.TeleportLureRespond (e.IM.FromAgentID, e.IM.IMSessionID, true);
				break;
			case InstantMessageDialog.FriendshipOffered:
				Console.WriteLine ("Friendship request from " + e.IM.FromAgentName + ": " + e.IM.Message);
				Client.Friends.AcceptFriendship (e.IM.FromAgentID, e.IM.IMSessionID);
				Client.Friends.GrantRights (e.IM.FromAgentID, FriendRights.CanSeeOnMap | FriendRights.CanSeeOnline);
				break;
			case InstantMessageDialog.MessageFromAgent:
				if (e.IM.ToAgentID != Client.Self.AgentID) {
					Console.WriteLine ("[group][" + e.IM.ToAgentID + "][" + e.IM.FromAgentName + "] " + e.IM.Message);
				} else if (isOwner (e.IM.FromAgentName)) {
					Console.WriteLine ("[command][" + e.IM.FromAgentName + "] " + e.IM.Message);
					Reply reply = delegate(string s) {
						Client.Self.InstantMessage (e.IM.FromAgentID, s, e.IM.IMSessionID);
					};
					processCommand (e.IM.Message, reply);
					log = false;
				} else {
					Console.WriteLine ("[IM][" + e.IM.FromAgentName + "] " + e.IM.Message);
				}
				break;
			default:
				Console.WriteLine ("Unknown IM type " + e.IM.Dialog + " from " + e.IM.FromAgentName + ": " + e.IM.Message);
				break;
			}
			if (log)
				dbw.logIMEvent (e);
		}

		private static void processCommand (string command, Reply reply)
		{
			command=command.Trim();
			var i = command.IndexOf (" ");
			string c, a = "";
			if (i >= 0) {
				c = command.Substring (0, i).Trim ();
				a = command.Substring (i + 1).Trim ();
			} else
				c = command;

			switch (c) {
			case "help":
				reply ("Commands: help logout nearby say shout whisper");
				break;
			case "logout":
				Client.Network.Logout();
				break;
			case "nearby":
				Vector3 selfpos = Client.Network.CurrentSim.AvatarPositions[Client.Self.AgentID];
				Client.Network.CurrentSim.ObjectsAvatars.ForEach (delegate(Avatar av) {
					if (av.ID == Client.Self.AgentID) return;
					Vector3 position = Client.Network.CurrentSim.AvatarPositions[av.ID];
					string message = av.Name + " @ " + position + (selfpos - position).Length() + "m";
					reply(message);
				});
				break;
			case "say":
				Client.Self.Chat (a, 0, ChatType.Normal);
				break;
			case "shout":
				Client.Self.Chat (a, 0, ChatType.Shout);
				break;
			case "whisper":
				Client.Self.Chat (a, 0, ChatType.Whisper);
				break;
			default:
				reply("Unknown command ");
				break;
			}
		}

		private static bool isOwner(String name)
		{
			return configuration ["owners"].Contains ("[" + name + "]");
		}
	}
}
