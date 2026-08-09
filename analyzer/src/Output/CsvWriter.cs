using System.Globalization;
using System.Reflection;

namespace BenchmarkAnalyzer.Output;

internal static class CsvWriter
{
	public static async Task WriteAsync<T>(string path, IEnumerable<T> rows)
	{
		var properties = GetPropertiesBaseFirst(typeof(T));
		await using var writer = new StreamWriter(path, false);
		await writer.WriteLineAsync(string.Join(',', properties.Select(property => Escape(property.Name))));

		foreach (var row in rows)
		{
			var values = properties.Select(property =>
				Escape(Convert.ToString(property.GetValue(row), CultureInfo.InvariantCulture) ?? ""));
			await writer.WriteLineAsync(string.Join(',', values));
		}
	}

	private static PropertyInfo[] GetPropertiesBaseFirst(Type type)
	{
		var hierarchy = new Stack<Type>();
		for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
			hierarchy.Push(current);

		return hierarchy
			.SelectMany(current => current.GetProperties(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
			.ToArray();
	}

	private static string Escape(string value) =>
		value.IndexOfAny([',', '"', '\r', '\n']) < 0
			? value
			: $"\"{value.Replace("\"", "\"\"")}\"";
}
