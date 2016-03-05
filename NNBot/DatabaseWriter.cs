using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using OpenMetaverse;

namespace NNBot
{
	public class DatabaseWriter
	{
		private MySqlConnection conn;
		private string connstring;
		private delegate void QueuedQuery();
		private BlockingCollection<QueuedQuery> queue = new BlockingCollection<QueuedQuery> ();
		private Task writerTask;

		public DatabaseWriter (string cs)
		{
			connstring = cs;
			reconnect (false);
			runWriter ();
		}

		private void reconnect(bool retry)
		{
			conn = new MySql.Data.MySqlClient.MySqlConnection ();
			conn.ConnectionString = connstring;
			while (true) {
				try {
					conn.Open ();
					return;
				} catch (MySql.Data.MySqlClient.MySqlException ex) {
					System.Console.WriteLine ("Mysql connect error: " + ex.Message);
					if (!retry)
						throw new Exception ("Database connection error", ex);
					Thread.Sleep (3000);
				}
			}
		}

		private void runWriter()
		{
			writerTask = Task.Run (() => {
				while(true) {
					QueuedQuery q = queue.Take();
					while(true) {
						try {
							q();
							break;
						} catch (MySql.Data.MySqlClient.MySqlException ex) {
							System.Console.WriteLine("Query error: " + ex.Message);
							conn.Close();
							Thread.Sleep (3000);
							reconnect(true);
						}
					}
				}
			});
		}

		public void logChat(string region, int type, string agentname, UUID agentuuid, string agentdisplayname, string botname, string message)
		{
			logChat (region, type, agentname, agentuuid, agentdisplayname, UUID.Zero, null, botname, message);
		}

		public void logChat(string region, int type, string agentname, UUID agentuuid, string agentdisplayname, UUID objectuuid, string objectname, string botname, string message)
		{
			queue.Add (() => {
				MySqlCommand cmd = conn.CreateCommand();
				cmd.CommandText = "INSERT INTO chat(region, type, agentname, agentuuid, agentdisplayname, objectuuid, objectname, botname, message)" + 
					"VALUES(?region, ?type, ?agentname, ?agentuuid, ?agentdisplayname, ?objectuuid, ?objectname, ?botname, ?message)";
				cmd.Parameters.Add("?region", MySqlDbType.VarChar).Value = region;
				cmd.Parameters.Add("?type", MySqlDbType.Int16).Value = type;
				cmd.Parameters.Add("?agentname", MySqlDbType.VarChar).Value = agentname;
				cmd.Parameters.Add("?agentuuid", MySqlDbType.VarChar).Value = agentuuid.ToString();
				cmd.Parameters.Add("?agentdisplayname", MySqlDbType.VarChar).Value = agentdisplayname;
				if (objectname != null) {
					cmd.Parameters.Add("?objectuuid", MySqlDbType.VarChar).Value = objectuuid.ToString();
					cmd.Parameters.Add("?objectname", MySqlDbType.VarChar).Value = objectname;
				} else {
					cmd.Parameters.AddWithValue("?objectuuid", DBNull.Value);
					cmd.Parameters.AddWithValue("?objectname", DBNull.Value);
				}
				cmd.Parameters.Add("?botname", MySqlDbType.VarChar).Value = botname;
				cmd.Parameters.Add("?message", MySqlDbType.VarChar).Value = message;
				cmd.ExecuteNonQuery();
			});
		}
	}
}

