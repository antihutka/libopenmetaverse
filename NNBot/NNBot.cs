﻿using System;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace NNBot
{
	public class Bot
	{
		public static GridClient Client;
		public static Dictionary<String,String> configuration;
		public static DatabaseWriter dbw;
		public static Random rand = new Random ();
		static ConversationHandler localchat;

		public static void Main(string[] args)
		{
			string configfile = "default.cfg";
			if (args.Length > 0)
				configfile = args [0];
			configuration = Config.LoadConfig (configfile);
			Console.WriteLine (string.Join(";",configuration));

			dbw = new DatabaseWriter (configuration ["dbstring"]);

			Client = new GridClient();
			if (configuration["loginserver"] != "") Client.Settings.LOGIN_SERVER = configuration["loginserver"];
			Client.Self.IM += new EventHandler<InstantMessageEventArgs> (IMHandler);
			Client.Self.ChatFromSimulator += new EventHandler<ChatEventArgs> (ChatHandler);
			Client.Avatars.UUIDNameReply += new EventHandler<UUIDNameReplyEventArgs> (UUIDNameHandler);
			Reply talklocal = (string s) => {
				Client.Self.Chat(s, 0, ChatType.Normal);
			};
			localchat = new ConversationHandler (UUID.Zero, talklocal);
			//debugEvents ();
			bool loggedIn = Client.Network.Login(configuration["firstname"], configuration["lastname"], configuration["password"], "NNBot", "NNBot 0.1");
			if (loggedIn) {
				Console.WriteLine ("Logged In");
				localchat.start ();
				Thread.Sleep (3000);
				attachStuff ();
			} else Console.WriteLine("Failed");
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
				Console.WriteLine ("[local][" + e.FromName + "] " + e.Message);
				ConversationHistory.getHistory (UUID.Zero).add (e.Message);
				if (e.SourceID != Client.Self.AgentID)
					localchat.incomingMessage (e.Message);
				break;
			default:
				Console.WriteLine ("Unknown chat type " + e.Type + " from " + e.FromName + ":" + e.Message);
				break;
			}

			if (log) {
				dbw.logChatEvent(e);
			}
		}

		private static void attachStuff()
		{
			UUID co = Client.Inventory.FindFolderForType (FolderType.CurrentOutfit);
			Console.WriteLine ("Current outfit folder: " + co);
			Thread.Sleep (1000);
			var coc = Client.Inventory.FolderContents (co, Client.Self.AgentID, true, true, InventorySortOrder.ByDate, 10000);
			Thread.Sleep (1000);
			if (coc == null) {
				Console.WriteLine ("FolderContents failed");
				return;
			}
			List<InventoryItem> coitems = new List<InventoryItem>();
			coc.ForEach((InventoryBase obj) => {
				if (obj is InventoryObject) {
					Console.WriteLine("Adding " + obj.Name);
					obj = Client.Inventory.FetchItem(((InventoryObject)obj).AssetUUID, Client.Self.AgentID, 10000);
					//Client.Appearance.Detach((InventoryObject)obj);
					//Thread.Sleep(5000);
					Client.Appearance.Attach((InventoryObject)obj, AttachmentPoint.Default, false);
					//Client.Appearance.AddAttachments (coitems, false);
					Thread.Sleep(1000);
					coitems.Clear();
				}
			});
			//Client.Appearance.AddAttachments (coitems, false);
		}

		public delegate void Reply(string s);

		public static Primitive findObjectInSim(UUID id)
		{
			return Client.Network.CurrentSim.ObjectsPrimitives.Find ((Primitive obj) => obj.ID == id);
		}

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
				if (isOwner (e.IM.FromAgentName) || isinlist(configuration["accepttp"], e.IM.FromAgentName))
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
						Thread.Sleep(3000);
					};
					processCommand (e.IM.Message, reply);
					log = false;
				} else {
					Console.WriteLine ("[IM <- " + e.IM.FromAgentName + "] " + e.IM.Message);
					ConversationHistory.getHistory (e.IM.FromAgentID).add (e.IM.Message);
					dbw.logIMEvent (e);
					log = false;
					string response = NNInterface.getLine (ConversationHistory.getHistory (e.IM.FromAgentID).get());
					ConversationHistory.getHistory (e.IM.FromAgentID).add (response);
					if (response.Equals ("")) {
						Console.WriteLine ("Tried to send empty IM to " + e.IM.FromAgentName);
						return;
					}
					//Thread.Sleep ((20 + e.IM.Message.Length + response.Length) * rand.Next (100, 400));
					Client.Self.InstantMessage (e.IM.FromAgentID, response, e.IM.IMSessionID);
					Console.WriteLine ("[IM -> " + e.IM.FromAgentName + "] " + response);
					dbw.logSentIM (e.IM.FromAgentID, e.IM.FromAgentName, response);
				}
				break;
			case InstantMessageDialog.MessageFromObject:
				Primitive o = findObjectInSim (e.IM.FromAgentID);
				UUID ownerid = (o == null) ? UUID.Zero : ObjPropGetter.getProperties(o).OwnerID;
				string owner = NameCache.getName(ownerid);
				Console.WriteLine ("[object][" + owner + "][" + e.IM.FromAgentName + "] " + e.IM.Message);
				ConversationHistory.getHistory (UUID.Zero).add (e.IM.Message);
				break;
			default:
				Console.WriteLine ("Unknown IM type " + e.IM.Dialog + " from " + e.IM.FromAgentName + ": " + e.IM.Message);
				break;
			}
			if (log) {
				dbw.logIMEvent (e);
			}
		}

		private static void listInventory (UUID folder, bool recurse, Reply reply, string search)
		{
			List<InventoryBase> cont = Client.Inventory.FolderContents (folder, Client.Self.AgentID, true, true, InventorySortOrder.ByDate, 10000);
			string messagec = "";
			cont.ForEach ((InventoryBase item) => {
				string message = item.Name + " " + item.GetType () + " " + item.UUID;
				if (item is InventoryItem) message += " " + ((InventoryItem)item).AssetUUID;
				if (search != null && !message.ToLower().Contains(search.ToLower())) return;
				messagec += message + "\n";
				if (messagec.Length > 512) {
					reply(messagec);
					messagec="";
				}
			});
			if (messagec != "") reply (messagec);
			if (recurse) {
				cont.ForEach ((InventoryBase item) => {
					if (item is InventoryFolder)
						listInventory (item.UUID, true, reply, search);
				});
			}
			if (!recurse && cont.Count == 0)
				reply ("(empty)");
		}

		private static void split(string str, string spliton, out string first, out string last)
		{
			var i = str.IndexOf (spliton);
			if (i >= 0) {
				first = str.Substring (0, i).Trim ();
				last = str.Substring (i + 1).Trim ();
			} else {
				first = str.Trim ();
				last = "";
			}
		}

		private static void processCommand (string command, Reply reply)
		{
			command=command.Trim();
			var i = command.IndexOf (" ");
			string c, a = "";
			char[] slash = { '/' };
			split (command, " ", out c, out a);

			Vector3 selfpos = Client.Network.CurrentSim.AvatarPositions[Client.Self.AgentID];

			switch (c) {
			case "help":
				reply ("Commands: help dumphistory getreply inventory logout nearby objects [attach child near] say shout sit stand status whisper");
				break;
			case "attach":
				var l = Client.Inventory.FetchItem (UUID.Parse (a), Client.Self.AgentID, 10000);
				Console.WriteLine ("Attaching " + l.Name);
				Client.Appearance.Attach ((InventoryItem)l, AttachmentPoint.Default, false);
				break;
			case "attachstuff":
				attachStuff ();
				break;
			case "detach":
				l = Client.Inventory.FetchItem (UUID.Parse (a), Client.Self.AgentID, 10000);
				Console.WriteLine ("Dettaching " + l.Name);
				Client.Appearance.Detach ((InventoryItem)l);
				break;
			case "dumphistory":
				ConversationHistory.dump (reply);
				break;
			case "getreply":
				reply (NNInterface.getLine (a + "\n"));
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
				bool own = Array.IndexOf (args, "own") >= 0;

				var prims = new List<Primitive> ();
				Client.Network.CurrentSim.ObjectsPrimitives.ForEach ((Primitive prim) => {
					prims.Add (prim);
				});

				string messagec = "";
				prims.ForEach ((Primitive prim) => {
					if (prim.ID == UUID.Zero)
						return;
					if (attach != prim.IsAttachment)
						return;
					if (!child && !attach && prim.ParentID != 0)
						return;

					double distance = (selfpos - prim.Position).Length ();

					if (near && distance > 20)
						return;

					string message = prim.ID.ToString ();
					message += " parent=" + prim.ParentID;
					message += " dist=" + distance.ToString ("#.00") + "m";

					Primitive.ObjectProperties prop = ObjPropGetter.getProperties (prim);
					if (prop != null) {
						message += " name=" + prop.Name;
						message += " owner=" + NameCache.getName (prop.OwnerID);
					} else
						message += " <timed out>";
					if (own && prop.OwnerID != Client.Self.AgentID) return;
					messagec += message + "\n";
					if (messagec.Length > 512) {
						reply (messagec);
						messagec = "";
					}
				});
				if (messagec != "")
					reply (messagec);
				break;
			case "say":
				Client.Self.Chat (a, 0, ChatType.Normal);
				break;
			case "set":
				string left, right;
				split (a, "=", out left, out right);
				configuration [left] = right;
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
				reply ("Avatars: " + Client.Network.CurrentSim.AvatarPositions.Count);
				reply ("Objects:" + Client.Network.CurrentSim.ObjectsPrimitives.Count);
				break;
			case "teleport":
				UUID lm_uuid;
				if (UUID.TryParse (a, out lm_uuid)) {
					Client.Self.Teleport (lm_uuid);
				} else
					Client.Self.Teleport (a, new Vector3 (128.0f, 128.0f, 128.0f));
				
				break;
			case "touch":
				if (UUID.TryParse (a, out lm_uuid)) {
					Primitive prim = Client.Network.CurrentSim.ObjectsPrimitives.Find ((Primitive obj) => obj.ID == lm_uuid);
					if (prim != null) {
						Client.Self.Touch (prim.LocalID);
						reply ("ok");
					}
				}
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
			return isinlist(configuration["owners"], name);
		}

		private static bool isinlist(string h, string n)
		{
			return h.ToLower ().Contains ("[" + n.ToLower() + "]");
		}
	}
}
