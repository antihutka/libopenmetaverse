using System;
using System.Collections.Generic;
using System.Threading;
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
			Client.Self.IM += new EventHandler<InstantMessageEventArgs> (IMHandler);
			Client.Self.ChatFromSimulator += new EventHandler<ChatEventArgs> (ChatHandler);
			Client.Avatars.UUIDNameReply += new EventHandler<UUIDNameReplyEventArgs> (UUIDNameHandler);
			//debugEvents ();
			bool loggedIn = Client.Network.Login(configuration["firstname"], configuration["lastname"], configuration["password"], "NNBot", "NNBot 0.1");
			if (loggedIn) Console.WriteLine("Logged In");
			else Console.WriteLine("Failed");
		}

		private static void debugEvents()
		{
			Client.Objects.ObjectProperties += new EventHandler<ObjectPropertiesEventArgs> ((object sender, ObjectPropertiesEventArgs e) => { Console.WriteLine("ObjectProperties for " + e.Properties.ObjectID);});
			Client.Objects.ObjectPropertiesUpdated += new EventHandler<ObjectPropertiesUpdatedEventArgs> ((object sender, ObjectPropertiesUpdatedEventArgs e) => { Console.WriteLine("ObjectPropertiesUpdated for " + e.Prim.ID);});
			Client.Objects.TerseObjectUpdate += new EventHandler<TerseObjectUpdateEventArgs> ((object sender, TerseObjectUpdateEventArgs e) => { Console.WriteLine ("TerseObjectUpdate for " + e.Prim.ID);});
			Client.Objects.ObjectPropertiesFamily += new EventHandler<ObjectPropertiesFamilyEventArgs> ((object sender, ObjectPropertiesFamilyEventArgs e) => { Console.WriteLine ("ObjectPropertiesFamilyEventArgs for " + e.Properties.ObjectID);});
			Client.Objects.ObjectDataBlockUpdate += new EventHandler<ObjectDataBlockUpdateEventArgs> ((object sender, ObjectDataBlockUpdateEventArgs e) => { Console.WriteLine("ObjectDataBlockUpdateEventArg for " + e.Prim.ID);});
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

		private static void listInventory (UUID folder, bool recurse, Reply reply, string search)
		{
			List<InventoryBase> cont = Client.Inventory.FolderContents (folder, Client.Self.AgentID, true, true, InventorySortOrder.ByDate, 10000);
			cont.ForEach ((InventoryBase item) => {
				string message = item.Name + " " + item.GetType () + " " + item.UUID;
				if (item is InventoryItem) message += " " + ((InventoryItem)item).AssetUUID;
				if (search != null && !message.ToLower().Contains(search.ToLower())) return;
				reply (message);
				Thread.Sleep(400);
			});
			if (recurse) {
				cont.ForEach ((InventoryBase item) => {
					if (item is InventoryFolder)
						listInventory (item.UUID, true, reply, search);
				});
			}
			if (!recurse && cont.Count == 0)
				reply ("(empty)");
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

			Vector3 selfpos = Client.Network.CurrentSim.AvatarPositions[Client.Self.AgentID];

			switch (c) {
			case "help":
				reply ("Commands: help inventory logout nearby objects [attach child near] say shout sit stand status whisper");
				break;
			case "inventory":
				
				UUID folderid;
				if (a == "") {
					listInventory (Client.Inventory.Store.RootFolder.UUID, false, reply, null);
				} else if (UUID.TryParse (a, out folderid)) {
					listInventory (folderid, false, reply, null);
				} else {
					listInventory (Client.Inventory.Store.RootFolder.UUID, true, reply, a);
				}
				break;
			case "logout":
				Client.Network.Logout();
				break;
			case "nearby":
				Client.Network.CurrentSim.ObjectsAvatars.ForEach (delegate(Avatar av) {
					if (av.ID == Client.Self.AgentID) return;
					Vector3 position = Client.Network.CurrentSim.AvatarPositions[av.ID];
					string message = av.Name + " @ " + position + (selfpos - position).Length() + "m";
					reply(message);
				});
				break;
			case "objects":
				var args = a.Split ();
				bool attach = Array.IndexOf (args, "attach") >= 0;
				bool child = Array.IndexOf (args, "child") >= 0;
				bool near = Array.IndexOf (args, "near") >= 0;

				var prims = new List<Primitive> ();
				Client.Network.CurrentSim.ObjectsPrimitives.ForEach ((Primitive prim) => {
					prims.Add (prim);
				});

				prims.ForEach ((Primitive prim) => {
					if (prim.ID == UUID.Zero) return;
					if (attach != prim.IsAttachment) return;
					if (!child && !attach && prim.ParentID != 0) return;

					double distance = (selfpos - prim.Position).Length();

					if (near && distance > 20) return;

					string message = prim.ID.ToString();
					message += " parent=" + prim.ParentID;
					message += " dist=" + distance.ToString("#.00") + "m";

					Primitive.ObjectProperties prop = ObjPropGetter.getProperties(prim);
					if (prop != null) {
						message += " name=" + prop.Name;
						message += " owner=" + NameCache.getName(prop.OwnerID);
					} else message += " <timed out>";
					reply(message);
					Thread.Sleep(200);
				});
				break;
			case "say":
				Client.Self.Chat (a, 0, ChatType.Normal);
				break;
			case "shout":
				Client.Self.Chat (a, 0, ChatType.Shout);
				break;
			case "sit":
				UUID arg = UUID.Zero;
				if (!UUID.TryParse (a, out arg)) {
					reply ("Invalid ID");
				} else {
					Client.Self.RequestSit (arg, Vector3.Zero);
					Client.Self.Sit ();
				}
				break;
			case "stand":
				Client.Self.Stand ();
				break;
			case "status":
				reply ("Location: " + Client.Network.CurrentSim.Name + " @ " + Client.Network.CurrentSim.AvatarPositions [Client.Self.AgentID]);
				reply ("Avatar: " + Client.Network.CurrentSim.AvatarPositions.Count);
				reply ("Objects:" + Client.Network.CurrentSim.ObjectsPrimitives.Count);
				break;
			case "teleport":
				UUID lm_uuid;
				if (UUID.TryParse (a, out lm_uuid)) {
					Client.Self.Teleport (lm_uuid);
				} else
					Client.Self.Teleport (a, new Vector3 (128.0f, 128.0f, 128.0f));
				
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
