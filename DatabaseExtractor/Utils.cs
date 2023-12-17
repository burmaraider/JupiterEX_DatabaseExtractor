using System;
using System.Text;

namespace DatabaseExtractor;

public static class Utils
{
	public static string GetStringFromTable(byte[] valueTable, int offset)
	{
		int num = Array.IndexOf(valueTable, (byte)0, offset);
		return Encoding.UTF8.GetString(valueTable, offset, num - offset);
	}

	public static string GetWStringFromTable(byte[] valueTable, int offset)
	{
		int num = Array.IndexOf(valueTable, (byte)0, offset);
		return Encoding.Unicode.GetString(valueTable, offset, num - offset);
	}
}
