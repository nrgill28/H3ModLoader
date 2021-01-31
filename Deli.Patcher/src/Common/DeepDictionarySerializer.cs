using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Deli
{
	public class DeepDictionaryConverter : JsonConverter
	{
		private delegate object? Reader(JToken root, JsonSerializer serializer);
		private delegate void Writer(JsonWriter writer, object value, JsonSerializer serializer);

		private static readonly MethodInfo _readerMethod;
		private static readonly MethodInfo _writerMethod;

		static DeepDictionaryConverter()
		{
			foreach (var method in typeof(DeepDictionaryConverter).GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
			{
				switch (method.Name)
				{
					case nameof(ReadJsonTyped):
						_readerMethod = method;
						break;
					case nameof(WriteJsonTyped):
						_writerMethod = method;
						break;
				}
			}

			if (_readerMethod is null || _writerMethod is null)
			{
				throw new InvalidOperationException("Reader or writer methods were not found.");
			}
		}

		private readonly Dictionary<Type, TypeCache> _caches = new();

		public override bool CanConvert(Type objectType)
		{
			return objectType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>) && i.GetGenericArguments()[0] == typeof(string));
		}

		private static Dictionary<string, TValue>? ReadJsonTyped<TValue>(JToken root, JsonSerializer serializer)
		{
			var ret = new Dictionary<string, TValue>();

			foreach (var token in root)
			{
				if (token is not JProperty property)
				{
					throw new InvalidOperationException("All tokens of a dictionary must be properties.");
				}

				ret.Add(property.Name, property.Value.ToObject<TValue>(serializer)!);
			}

			return ret;
		}

		private static void WriteJsonTyped<TValue>(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var dict = (IDictionary<string, TValue>) value;

			writer.WriteStartObject();
			foreach (var pair in dict)
			{
				writer.WritePropertyName(pair.Key);
				serializer.Serialize(writer, pair.Value);
			}
			writer.WriteEndObject();
		}

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			var dictType = value.GetType();
			if (!_caches.TryGetValue(dictType, out var cache))
			{
				_caches.Add(dictType, new TypeCache(dictType));
			}

			cache.Writer(writer, value, serializer);
		}

		public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
		{
			var root = JToken.ReadFrom(reader);

			if (root is JValue {Value: null})
			{
				return null;
			}

			if (root is not JObject)
			{
				throw new InvalidOperationException("Expected a dictionary object.");
			}

			if (!_caches.TryGetValue(objectType, out var cache))
			{
				cache = new TypeCache(objectType);
				_caches.Add(objectType, cache);
			}

			return cache.Reader(root, serializer);
		}

		private class TypeCache
		{
			public readonly Reader Reader;
			public readonly Writer Writer;

			public TypeCache(Type dictType)
			{
				var valueType = dictType.GetGenericArguments()[1];
				Reader = (Reader) Delegate.CreateDelegate(typeof(Reader), _readerMethod.MakeGenericMethod(valueType));
				Writer = (Writer) Delegate.CreateDelegate(typeof(Writer), _writerMethod.MakeGenericMethod(valueType));
			}
		}
	}
}
