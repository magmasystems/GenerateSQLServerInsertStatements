using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.SqlServer.Types;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace GenerateSQLServerInsertStatements
{
	/// <summary>
	/// This was written by Marc Adler (magmasystems at yahoo dot com).
	/// This code can be used freely for your own personal use.
	/// It may not be reprinted anywhere without permission of Marc Adler.
	/// 
	/// Here is a sample command line
	/// -connectionstring "Data Source=localhost;Integrated Security=True;initial catalog=PipelineTariffTool" -database PipelineTariffTool -deleteall -tables * -output "C:\temp\Create {t}.sql"
	/// 
	/// </summary>
	internal static class Program
	{
		// ReSharper disable once InconsistentNaming
		private static List<string> dataTypeNames;

		[STAThread]
		private static void Main(string[] args)
		{
			string databaseName = null;
			string tableNames = null;
			var connectionString = "Data Source=(local);Initial Catalog={d};Integrated Security=True";
			var outputFile = "./GeneratedStatements.sql";
			var deleteAll = false;
			var append = false;
			var noIdentity = false;


			if (args == null || args.Length == 0)
			{
				Usage();
				return;
			}

			for (var i = 0; i < args.Length; i++)
			{
				switch (args[i].ToLower())
				{
					case "-tables":
						tableNames = args[++i];
						break;
					case "-database":
						databaseName = args[++i];
						break;
					case "-output":
						outputFile = args[++i];
						break;
					case "-connectionstring":
						connectionString = args[++i];
						break;
					case "-deleteall":
						deleteAll = true;
						break;
					case "-append":
						append = true;
						break;
					case "-noidentity":
						noIdentity = true;
						break;
					default:
						Usage();
						return;
				}
			}

			if (string.IsNullOrEmpty(tableNames) || string.IsNullOrEmpty(databaseName))
			{
				Console.WriteLine("The table name or the database name was not specified.\n");
				Usage();
				return;
			}

			if (connectionString.Contains("{d}"))
				connectionString = connectionString.Replace("{d}", databaseName);

			if (tableNames == "*")
			{
				tableNames = string.Join(",", GetAllTableNameInDatabase(connectionString));
			}

			try
			{
				var tables = tableNames.Split(new[] {','});
				foreach (var table in tables)
				{
					GetData(databaseName, table, connectionString, outputFile, deleteAll, append, noIdentity);
					if (tables.Length > 1)
						append = true;
				}
			}
			catch (Exception exc)
			{
				Console.WriteLine(exc.Message);
			}

			Console.WriteLine("Press ENTER to quit");
			Console.ReadLine();
		}

		private static void Usage()
		{
			Console.WriteLine(
				"SQLGenerateInsertStatements [-help] [-tables <tablenames>] [-database <databasename>] [-connectionstring <connstring>] [-output <outputfile>] [-deleteall] [-append] [-noidentity]");
			Console.WriteLine("The default output file is GeneratedStatements.sql");
			Console.WriteLine("If the connection string has '{db}' embedded in it, the '{d}' is replaced with the database name.");
			Console.WriteLine("If the outputfile string has '{d}' embedded in it, the '{d}' is replaced with today's date.");
			Console.WriteLine("If the outputfile string has '{t}' embedded in it, the '{t}' is replaced with the table name.");
		}

		private static void GetData(string databaseName, string tableName, string connectionString, string outputFile,
		                            bool deleteAll, bool append, bool noIdentity)
		{
			// Get the (optional) name of the file to write the SQL statements to
			if (outputFile.IndexOf("{d}", StringComparison.Ordinal) >= 0)
			{
				outputFile = outputFile.Replace("{d}", DateTime.Now.ToString("d", new CultureInfo("de-DE")));
			}
			if (outputFile.IndexOf("{t}", StringComparison.Ordinal) >= 0)
			{
				outputFile = outputFile.Replace("{t}", tableName);
			}
			if (outputFile.IndexOf("{db}", StringComparison.Ordinal) >= 0)
			{
				outputFile = outputFile.Replace("{db}", databaseName);
			}
			var outputStream = new StreamWriter(outputFile, append);

			// Write the "USE database" statement
			outputStream.WriteLine($"USE [{databaseName}]");

			// Maybe write the DELETE statement
			if (deleteAll)
				outputStream.WriteLine($"DELETE FROM [dbo].[{tableName}]");
			if (noIdentity == false)
				outputStream.WriteLine($"SET IDENTITY_INSERT [dbo].[{tableName}] ON");

			var nLines = 1;

			using (var connection = new SqlConnection(connectionString))
			{
				using (var command = new SqlCommand())
				{
					// Initialize the SQL Connection
					command.Connection = connection;
					command.CommandText = $"SELECT * FROM [dbo].[{tableName}]";
					connection.Open();

					// Get a DataReader
					using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
					{
						dataTypeNames = new List<string>();

						// Format the first part of the INSERT statement. This part remains 
						// constant for every row that is generated.
						var sInsert = $"INSERT INTO [dbo].[{tableName}] ( ";
						for (var iCol = 0; iCol < reader.FieldCount; iCol++)
						{
							sInsert += $"[{reader.GetName(iCol)}]";
							if (iCol < reader.FieldCount - 1)
								sInsert += ",";
							dataTypeNames.Add(reader.GetDataTypeName(iCol));
						}
						sInsert += ") VALUES ({0})";

						// Read each row of the table
						var objs = new object[reader.FieldCount];

						while (reader.Read())
						{
							var n = reader.GetValues(objs);
							var sValues = string.Empty;

							// Go through each column of the row, and generate a string
							for (var i = 0; i < n; i++)
							{
								try
								{
									var sVal = (reader.IsDBNull(i)) ? "null" : ObjectToSqlString(objs[i], dataTypeNames[i]);
									sValues += sVal;
									if (i < n - 1)
										sValues += ",";
								}
								catch (DataException)
								{
									Console.WriteLine(
										$"Conversion error in Record {nLines}, Column {reader.GetName(i)}");
									return;
								}
							}

							// Dump the INSERT statement to the file
							outputStream.WriteLine(sInsert, sValues);
							nLines++;
						}
					}
				}
			}

			if (noIdentity == false)
				outputStream.WriteLine($"SET IDENTITY_INSERT [dbo].[{tableName}] OFF");
			outputStream.Flush();
			outputStream.Close();

			Console.WriteLine($"Finshed generating {nLines - 1} rows for table {tableName}");
		}

		private static string ObjectToSqlString(object o, string dataTypeName)
		{
			if (o == null || o == DBNull.Value)
				return "null";

			var t = o.GetType();

			if (t == typeof (string))
			{
				var s = ((string) o).Trim().Replace("'", "''");
				return $"'{s}'";
			}

			if (t == typeof (int))
				return ((int) o).ToString();
			if (t == typeof (long))
				return ((long) o).ToString();
			if (t == typeof (float))
				return ((float) o).ToString(CultureInfo.InvariantCulture);
			if (t == typeof (double))
				return ((double) o).ToString(CultureInfo.InvariantCulture);
			if (t == typeof (DateTime))
				return $"'{((DateTime)o)}'";
			if (t == typeof (bool))
				return ((bool) o) ? "1" : "0";
			if (t == typeof (decimal))
				return ((decimal) o).ToString(CultureInfo.InvariantCulture);

			// WARNING - We must reference the Microsoft.SqlServer.Types.dll from SQL Server version 10, not version 11.
			// The path of this DLL is C:\Program Files (x86)\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.Types.dll
			if (t == typeof (SqlGeography))
			{
				// Need to generate a string like this: geography::STPointFromText('POINT(-90.098244 38.862309)', 4326)
				SqlGeography geography = (SqlGeography) o;
				return $"geography::STPointFromText('POINT({geography.Long} {geography.Lat})', 4326)";
			}

			// There are row timestamps that we cannot insert into a table. So just return NULL and let SQL Server take care of it.
			if (t == typeof (byte[]))
			{
				if (dataTypeName == "timestamp")
					return "NULL";
				return Encoding.UTF8.GetString((byte[]) o);
			}

			throw new DataException("Cannot process the .NET type " + t.Name);
		}

		private static IEnumerable<string> GetAllTableNameInDatabase(string connectionString)
		{
			var tableNames = new List<string>();

			using var connection = new SqlConnection(connectionString);
			using var command = new SqlCommand();
			
			// Initialize the SQL Connection
			command.Connection = connection;
			command.CommandText = "SELECT name FROM Sys.Tables ORDER BY name";
			connection.Open();

			// Get a DataReader
			using var reader = command.ExecuteReader(CommandBehavior.CloseConnection);
			while (reader.Read())
			{
				tableNames.Add((string) reader[0]);
			}

			return tableNames;
		}
	}
}
