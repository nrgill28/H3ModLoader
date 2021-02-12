using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx.Logging;
using Deli.Immediate;
using Deli.Patcher;
using Deli.VFS;
using Deli.Newtonsoft.Json;
using Deli.Newtonsoft.Json.Linq;
using Semver;

namespace Deli
{
	/// <summary>
	///		An element of the loading sequence. This class is not intended to be inherited outside the framework, so please don't.
	/// </summary>
	public abstract class Stage
	{
		private readonly Blob _data;

		private ImmediateReaderCollection JsonReaders => _data.JsonReaders;

#pragma warning disable CS1591

		protected JsonSerializer Serializer => _data.Serializer;

		protected Mod Mod => _data.Mod;

		protected ManualLogSource Logger => Mod.Logger;

		protected Dictionary<Mod, List<DeliModule>> ModModules => _data.ModModules;

		protected abstract string Name { get; }

		protected LocaleFormatter Locale { get; }

#pragma warning restore CS1591

		/// <summary>
		///		The collection of all the <see cref="ImmediateAssetLoader{TStage}"/>s registered.
		/// </summary>
		public NestedServiceCollection<Mod, string, ImmediateAssetLoader<Stage>> SharedAssetLoaders => _data.SharedAssetLoaders;

		/// <summary>
		///		The collection of all the <see cref="ImmediateReader{T}"/>s publicly available.
		/// </summary>
		public ImmediateReaderCollection ImmediateReaders => _data.ImmediateReaders;

#pragma warning disable CS1591

		protected Stage(Blob data)
		{
			_data = data;

			Locale = new(this);
		}

		protected IEnumerable<IHandle> Glob(Mod mod, KeyValuePair<string, AssetLoaderID> asset)
		{
			var glob = asset.Key;
			var loader = asset.Value;

			Logger.LogDebug($"Enumerating glob: {glob}");
			using var globbed = mod.Resources.Glob(glob).GetEnumerator();

			if (!globbed.MoveNext())
			{
				Logger.LogWarning($"Asset glob from {mod} of type {loader} did not match any handles: {glob}");
				yield break;
			}

			do
			{
				var handle = globbed.Current!;

				Logger.LogDebug($"{handle} > {loader}");
				yield return handle;
			} while (globbed.MoveNext());
		}

		protected Mod.Manifest ModManifestOf(IFileHandle file)
		{
			var obj = ImmediateReaders.Get<JObject>()(file);

			// Do early version checking before deserializing the whole object.
			const string propertyName = "require";
			var property = obj[propertyName];
			if (property is null)
			{
				throw new FormatException("Manifest must have a '" + propertyName + "' property that describes the version of Deli required.");
			}

			var require = property.ToObject<SemVersion?>();
			if (require is null)
			{
				throw new FormatException("The required Deli version must not be null.");
			}

			var deli = Bootstrap.Constants.Metadata.SemVersion;
			if (!deli.Satisfies(require))
			{
				throw new FormatException($"This mod is incompatible with the current version of Deli (required: {require}; current: {deli})");
			}

			return obj.ToObject<Mod.Manifest>(Serializer)!;
		}

		[return: MaybeNull]
		protected T JsonOf<T>(IFileHandle file)
		{
			var token = ImmediateReaders.Get<JToken>()(file);

			return token.ToObject<T>(Serializer);
		}

		protected static IFileHandle AssemblyPreloader(IHandle handle)
		{
			if (handle is not IFileHandle file)
			{
				throw new ArgumentException("Assembly loaders must be provided an assembly file.", nameof(handle));
			}

			return file;
		}

		protected virtual void TypeLoader(Stage stage, Mod mod, Type type)
		{
			if (type.IsAbstract || !typeof(DeliModule).IsAssignableFrom(type)) return;

			var module = (DeliModule) Activator.CreateInstance(type, mod);

			if (!ModModules.TryGetValue(mod, out var modules))
			{
				modules = new List<DeliModule>();
				ModModules.Add(mod, modules);
			}
			modules.Add(module);

			try
			{
				module.Run(stage);
			}
			catch
			{
				Logger.LogFatal(Locale.PluginException(mod, "module"));
				throw;
			}
		}

		protected virtual void AssemblyLoader(Stage stage, Mod mod, Assembly assembly)
		{
			foreach (var type in assembly.GetExportedTypes())
			{
				TypeLoader(stage, mod, type);
			}
		}

		protected void RunModules(Mod mod)
		{
			const string pluginType = "module";
			if (!ModModules.TryGetValue(mod, out var modules)) return;

			Logger.LogDebug(Locale.LoadingPlugin(mod, pluginType));
			foreach (var module in modules)
			{
				try
				{
					module.Run(this);
				}
				catch
				{
					Logger.LogFatal(Locale.PluginException(mod, pluginType));
					throw;
				}
			}
		}

		protected void PreRun()
		{
			Logger.LogDebug($"Running the {Name} stage...");
		}

#pragma warning restore CS1591

		/// <summary>
		///		Creates and adds a JSON <see cref="ImmediateReader{T}"/> for the type provided.
		/// </summary>
		/// <typeparam name="T">The JSON model.</typeparam>
		public ImmediateReader<T> RegisterJson<T>() where T : notnull
		{
			if (JsonReaders.TryGet<T>(out var reader))
			{
				return reader;
			}

			reader = JsonOf<T>;
			JsonReaders.Add(reader);
			ImmediateReaders.Add(reader);

			return reader;
		}

		/// <summary>
		///		Data passed between each stage in the framework
		/// </summary>
		public readonly struct Blob
		{
			internal Mod Mod { get; }
			internal ImmediateReaderCollection JsonReaders { get; }
			internal JsonSerializer Serializer { get; }
			internal NestedServiceCollection<Mod, string, ImmediateAssetLoader<Stage>> SharedAssetLoaders { get; }
			internal ImmediateReaderCollection ImmediateReaders { get; }
			internal Dictionary<Mod, List<DeliModule>> ModModules { get; }

			internal Blob(Mod mod, ImmediateReaderCollection jsonReaders, JsonSerializer serializer,
				NestedServiceCollection<Mod, string, ImmediateAssetLoader<Stage>> sharedAssetLoaders, ImmediateReaderCollection immediateReaders,
				Dictionary<Mod, List<DeliModule>> modModules)
			{
				Mod = mod;
				JsonReaders = jsonReaders;
				Serializer = serializer;
				SharedAssetLoaders = sharedAssetLoaders;
				ImmediateReaders = immediateReaders;
				ModModules = modModules;
			}
		}

#pragma warning disable CS1591

		protected class LocaleFormatter
		{
			private readonly Stage _stage;

			public LocaleFormatter(Stage stage)
			{
				_stage = stage;
			}

			public string LoadingPlugin(Mod mod, string pluginType) => $"Loading {mod} {pluginType}s into {_stage.Name}...";
			public string LoadingAssets(Mod mod) => $"Loading {_stage.Name} assets from {mod}...";

			public string LoaderException(AssetLoaderID loader, Mod mod, Mod targetMod, IHandle targetHandle) => $"{loader} from {mod} threw an exception while loading a {_stage.Name} asset from {targetMod}: {targetHandle}";
			public string PluginException(Mod mod, string pluginType) => $"A {pluginType} from {mod} threw an exception during {_stage.Name} stage.";
		}

#pragma warning restore CS1591
	}

	/// <summary>
	///		An element of the loading sequence which uses specific loaders. This class is not intended to be inherited outside the framework, so please don't.
	/// </summary>
	/// <typeparam name="TLoader">The type of the loaders to use</typeparam>
	public abstract class Stage<TLoader> : Stage where TLoader : Delegate
	{
#pragma warning disable CS1591

		protected Stage(Blob data) : base(data)
		{
		}

		protected abstract TLoader? GetLoader(Mod mod, string name);

		protected TLoader GetLoader(Mod mod, Dictionary<string, Mod> lookup, KeyValuePair<string, AssetLoaderID> asset, out Mod loaderMod)
		{
			var loaderID = asset.Value;

			if (!lookup.TryGetValue(loaderID.Mod, out loaderMod))
			{
				throw new InvalidOperationException($"Mod required for {Name} asset \"{asset.Key}\" of {mod} was not present: {loaderID.Mod}");
			}

			var loader = GetLoader(loaderMod, loaderID.Name);
			if (loader is null)
			{
				throw new InvalidOperationException($"Loader required for {Name} asset \"{asset.Key}\" of {mod} was not present.");
			}

			return loader;
		}

#pragma warning restore CS1591
	}
}