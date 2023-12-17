using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;
using IniParser;
using IniParser.Model;

namespace DatabaseExtractor;

internal static class Program
{
	private class DatabaseHeader
	{
		public string Signature;

		public int Version;

		public int ValueTableLength;

		public int Unknown1Length;

		public int Unknown2Length;

		public int Unknown3Length;

		public int Unknown4Length;
	}

	private class DatabaseCategory
	{
		public string Name;

		public List<DatabaseRecord> Records = new List<DatabaseRecord>();
	}

	private class DatabaseRecord
	{
		public string Name;

		public List<DatabaseAttribute> Attributes = new List<DatabaseAttribute>();
	}

	private enum AttributeType
	{
		Invalid,
		Bool,
		Float,
		Int32,
		String,
		WString,
		Vector2,
		Vector3,
		Vector4,
		RecordLink,
		Struct
	}

	private enum AttributeUsage
	{
		Default,
		Filename,
		ClientFX,
		Animation
	}

	private class DatabaseAttribute
	{
		public AttributeType AttributeType;

		public AttributeUsage AttributeUsage;

		public string Name;

		public List<dynamic> Values = new List<object>();
	}

	private class RecordLink
	{
		public short RecordIndex;

		public short CategoryIndex;
	}

	private class Vector
	{
		public float W;

		public float X;

		public float Y;

		public float Z;
		
		public AttributeType attrType;

		public static Vector FromTable(AttributeType attributeType, byte[] valueTable, int offset)
		{
			float w = 0f;
			float x = 0f;
			float y = 0f;
			float z = 0f;
			switch (attributeType)
			{
			case AttributeType.Vector2:
				x = BitConverter.ToSingle(valueTable, offset);
				y = BitConverter.ToSingle(valueTable, offset + 4);
				break;
			case AttributeType.Vector3:
				x = BitConverter.ToSingle(valueTable, offset);
				y = BitConverter.ToSingle(valueTable, offset + 4);
				z = BitConverter.ToSingle(valueTable, offset + 8);
				break;
			case AttributeType.Vector4:
				w = BitConverter.ToSingle(valueTable, offset);
				x = BitConverter.ToSingle(valueTable, offset + 4);
				y = BitConverter.ToSingle(valueTable, offset + 8);
				z = BitConverter.ToSingle(valueTable, offset + 12);
				break;
			default:
				throw new ArgumentException();
			}
			return new Vector
			{
				W = w,
				X = x,
				Y = y,
				Z = z,
                attrType = attributeType
            };
		}

		public override string ToString()
		{
			if (attrType == AttributeType.Vector2)
			{
                return $"{X:F6}, {Y:F6}, 0.000000, 0.000000";
            }

            else if (attrType == AttributeType.Vector3)
            {
                return $"{X:F6}, {Y:F6}, {Z:F6}, 0.000000";
            }

            return $"{W:F6}, {X:F6}, {Y:F6}, {Z:F6}";
		}
	}

	private static DatabaseHeader _databaseHeader = new DatabaseHeader();

	private static List<DatabaseCategory> _databaseCategories = new List<DatabaseCategory>();

	private static void Main(string[] args)
	{
		if (args.Length >= 1)
		{
			string text = args[0];
			if (!File.Exists(text))
			{
				Console.WriteLine(text + " File not found.");
				return;
			}
			if(OpenDatabase(text))
            {
                Console.WriteLine("\nDatabase opened successfully. Converting...");
            }
			else
			{
                Console.WriteLine("Invalid database file.");
				return;
            }
            SaveDatabase(text);
			Console.WriteLine("Database successfully saved.");
		}
        else
        {
            Console.WriteLine("Usage: DatabaseExtractor.exe <database file>");
			return;
        }
    }

	private static bool OpenDatabase(string filePath)
	{
		using BinaryReader binaryReader = new BinaryReader(File.OpenRead(filePath));
		_databaseHeader = new DatabaseHeader
		{
			Signature = new string(binaryReader.ReadChars(4)),
			Version = binaryReader.ReadInt32(),
			ValueTableLength = binaryReader.ReadInt32(),
			Unknown1Length = binaryReader.ReadInt32(),
			Unknown2Length = binaryReader.ReadInt32(),
			Unknown3Length = binaryReader.ReadInt32(),
			Unknown4Length = binaryReader.ReadInt32()
		};
		if (_databaseHeader.Signature != "GADB" || _databaseHeader.Version != 3)
		{
			return false;
		}
		byte[] valueTable = binaryReader.ReadBytes(_databaseHeader.ValueTableLength);
		int num = binaryReader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			DatabaseCategory databaseCategory = new DatabaseCategory();
			databaseCategory.Name = Utils.GetStringFromTable(valueTable, binaryReader.ReadInt32());
			int num2 = binaryReader.ReadInt32();
			for (int j = 0; j < num2; j++)
			{
				DatabaseRecord databaseRecord = new DatabaseRecord();
				databaseRecord.Name = Utils.GetStringFromTable(valueTable, binaryReader.ReadInt32());
				int num3 = binaryReader.ReadInt32();
				for (int k = 0; k < num3; k++)
				{
					DatabaseAttribute databaseAttribute = new DatabaseAttribute();
					databaseAttribute.Name = Utils.GetStringFromTable(valueTable, binaryReader.ReadInt32());
					databaseAttribute.AttributeType = (AttributeType)binaryReader.ReadInt32();
					databaseAttribute.AttributeUsage = (AttributeUsage)binaryReader.ReadInt32();
					int num4 = binaryReader.ReadInt32();
					for (int l = 0; l < num4; l++)
					{
						dynamic val = null;
						val = databaseAttribute.AttributeType switch
						{
							AttributeType.Bool => binaryReader.ReadInt32() != 0, 
							AttributeType.Float => binaryReader.ReadSingle(), 
							AttributeType.Int32 => binaryReader.ReadInt32(), 
							AttributeType.String => Utils.GetStringFromTable(valueTable, binaryReader.ReadInt32()), 
							AttributeType.WString => Utils.GetWStringFromTable(valueTable, binaryReader.ReadInt32()), 
							AttributeType.Vector2 => Vector.FromTable(databaseAttribute.AttributeType, valueTable, binaryReader.ReadInt32()), 
							AttributeType.Vector3 => Vector.FromTable(databaseAttribute.AttributeType, valueTable, binaryReader.ReadInt32()), 
							AttributeType.Vector4 => Vector.FromTable(databaseAttribute.AttributeType, valueTable, binaryReader.ReadInt32()), 
							AttributeType.RecordLink => new RecordLink
							{
								RecordIndex = binaryReader.ReadInt16(),
								CategoryIndex = binaryReader.ReadInt16()
							}, 
							AttributeType.Struct => binaryReader.ReadInt32(), 
							_ => throw new ArgumentException(), 
						};
						((List<object>)databaseAttribute.Values).Add(val);
					}
					databaseRecord.Attributes.Add(databaseAttribute);
				}
				databaseCategory.Records.Add(databaseRecord);
			}
			_databaseCategories.Add(databaseCategory);
			Console.Write("[" + databaseCategory.Name + "]...");
		}
		return true;
	}

	private static void SaveDatabase(string filePath)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
		string directoryName = Path.GetDirectoryName(filePath);
		FileIniDataParser fileIniDataParser = new FileIniDataParser();
		IniData iniData = new IniData();
		iniData.Configuration.AssigmentSpacer = string.Empty;
		KeyDataCollection keyDataCollection = iniData["General"];
		keyDataCollection["Name"] = fileNameWithoutExtension;
		keyDataCollection["Build"] = fileNameWithoutExtension + ".Gamdb00p";
		keyDataCollection["Base"] = "..\\";
		keyDataCollection["Game"] = "..\\..\\" + fileNameWithoutExtension + "exe";
		keyDataCollection["GameServer"] = "..\\GameServer.dll";
		keyDataCollection["StringDatabase"] = "..\\StringDatabase\\" + fileNameWithoutExtension + ".Strdb00p";
		keyDataCollection["Version"] = _databaseHeader.Version.ToString();
		keyDataCollection["Database"] = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
		fileIniDataParser.WriteFile(directoryName + Path.DirectorySeparatorChar + Path.ChangeExtension(fileNameWithoutExtension, ".Gamdb00s"), iniData, Encoding.Unicode);
		foreach (DatabaseCategory databaseCategory2 in _databaseCategories)
		{
			string text = Path.Combine(directoryName, databaseCategory2.Name);
			if (Directory.Exists(text))
			{
				continue;
			}
			Directory.CreateDirectory(text);
			IniData iniData2 = new IniData();
			iniData2.Configuration.AssigmentSpacer = string.Empty;
			KeyDataCollection keyDataCollection2 = iniData2["Category"];
			keyDataCollection2["Schema"] = ((databaseCategory2.Records.Count == 0) ? string.Empty : databaseCategory2.Name.Replace('/', '.'));
			keyDataCollection2["Comment"] = string.Empty;
			keyDataCollection2["Help"] = string.Empty;
			keyDataCollection2["System"] = "False";
			fileIniDataParser.WriteFile(text + Path.DirectorySeparatorChar + "gdb.category", iniData2, Encoding.Unicode);
			if (databaseCategory2.Records.Count == 0)
			{
				continue;
			}
			string fileName = Path.GetFileName(databaseCategory2.Name);
			IniData iniData3 = new IniData();
			iniData3.Configuration.AssigmentSpacer = string.Empty;
			KeyDataCollection keyDataCollection3 = iniData3["Schema"];
			keyDataCollection3["Name"] = databaseCategory2.Name.Replace('/', '.');
			keyDataCollection3["Help"] = string.Empty;
			keyDataCollection3["Parent"] = string.Empty;
			foreach (DatabaseRecord record in databaseCategory2.Records)
			{
				IniData iniData4 = new IniData();
				iniData4.Configuration.AssigmentSpacer = string.Empty;
				KeyDataCollection keyDataCollection4 = iniData4["Record"];
				keyDataCollection4["Schema"] = databaseCategory2.Name.Replace('/', '.');
				keyDataCollection4["Name"] = record.Name;
				keyDataCollection4["Comment"] = "";
				keyDataCollection4["VirtualRelativeCategory"] = "";
				foreach (DatabaseAttribute attribute in record.Attributes)
				{
					KeyDataCollection keyDataCollection5 = iniData3["Attrib." + attribute.Name + ".0"];
					keyDataCollection5["Type"] = "";
					keyDataCollection5["Data"] = "";
					keyDataCollection5["Default"] = "";
					keyDataCollection5["Unicode"] = "False";
					keyDataCollection5["Deleted"] = "False";
					keyDataCollection5["Inherit"] = "True";
					keyDataCollection5["Values"] = attribute.Values.Count.ToString();
					keyDataCollection5["Help"] = "";
					switch (attribute.AttributeType)
					{
					case AttributeType.Bool:
						keyDataCollection5["Type"] = "Boolean";
						break;
					case AttributeType.Float:
						keyDataCollection5["Type"] = "Float";
						break;
					case AttributeType.Int32:
						keyDataCollection5["Type"] = "Integer";
						break;
					case AttributeType.String:
						keyDataCollection5["Type"] = "Text";
						break;
					case AttributeType.WString:
						keyDataCollection5["Type"] = "Text";
						keyDataCollection5["Unicode"] = "True";
						break;
					case AttributeType.Vector2:
						keyDataCollection5["Type"] = "Vector2";
						break;
					case AttributeType.Vector3:
						keyDataCollection5["Type"] = "Vector3";
						break;
					case AttributeType.Vector4:
						keyDataCollection5["Type"] = "Vector4";
						break;
					case AttributeType.RecordLink:
						keyDataCollection5["Type"] = "RecordLink";
						break;
					case AttributeType.Struct:
						keyDataCollection5["Type"] = "Float";
						break;
					}
					KeyDataCollection keyDataCollection6 = iniData4["Attrib." + attribute.Name];
					keyDataCollection6["Inherit"] = "False";
					keyDataCollection6["PlaceHolder"] = "False";
					keyDataCollection6["Todo"] = "False";
					keyDataCollection6["Modified"] = "False";
					keyDataCollection6["Override"] = "False";
					keyDataCollection6["Lock"] = "";
					keyDataCollection6["Comment"] = "";
					for (int i = 0; i < attribute.Values.Count; i++)
					{
						switch (attribute.AttributeType)
						{
						case AttributeType.Bool:
							keyDataCollection6[$"Value.{i:D4}"] = ((attribute.Values[i]) ? "True" : "False");
							break;
						case AttributeType.Float:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i].ToString();
							break;
						case AttributeType.Int32:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i].ToString();
							break;
						case AttributeType.String:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i];
							break;
						case AttributeType.WString:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i];
							break;
						case AttributeType.Vector2:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i].ToString();
							break;
						case AttributeType.Vector3:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i].ToString();
							break;
						case AttributeType.Vector4:
							keyDataCollection6[$"Value.{i:D4}"] = attribute.Values[i].ToString();
							break;
						case AttributeType.RecordLink:
						{
							RecordLink recordLink = attribute.Values[i] as RecordLink;
							if (recordLink.RecordIndex == -1 || recordLink.CategoryIndex == -1)
							{
								keyDataCollection6[$"Value.{i:D4}"] = "<none>";
								break;
							}
							DatabaseCategory databaseCategory = _databaseCategories[recordLink.CategoryIndex];
							DatabaseRecord databaseRecord = databaseCategory.Records[recordLink.RecordIndex];
							keyDataCollection6[$"Value.{i:D4}"] = databaseCategory.Name + "/" + databaseRecord.Name;
							break;
						}
						}
					}
				}
				fileIniDataParser.WriteFile(text + Path.DirectorySeparatorChar + record.Name + ".record", iniData4, Encoding.Unicode);
			}
			fileIniDataParser.WriteFile(text + Path.DirectorySeparatorChar + fileName + ".schema", iniData3, Encoding.Unicode);
		}
	}
}
