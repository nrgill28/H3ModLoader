using System.Diagnostics.CodeAnalysis;
using BepInEx.Logging;

namespace Deli.Runtime
{
	public class DelayedReaderCollection : ServiceCollection
	{
		public DelayedReaderCollection(ManualLogSource logger) : base(logger)
		{
		}

		public void Add<T>(DelayedReader<T> reader) where T : notnull
		{
			Add(typeof(T), reader);
		}

		public DelayedReader<T> Get<T>() where T : notnull
		{
			return (DelayedReader<T>) Get(typeof(T));
		}

		public bool TryGet<T>([MaybeNullWhen(false)] out DelayedReader<T> reader) where T : notnull
		{
			if (Services.TryGetValue(typeof(T), out var obj))
			{
				reader = (DelayedReader<T>) obj;
				return true;
			}

			reader = null;
			return false;
		}
	}
}
