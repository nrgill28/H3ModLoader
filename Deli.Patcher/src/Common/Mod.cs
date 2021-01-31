using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using Deli.VFS;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Deli
{
	/// <summary>
	///		Represents a Deli mod and contains all the resources available to it.
	/// </summary>
    public class Mod
    {
	    /// <summary>
	    ///		The manifest file contained by the mod on disk.
	    /// </summary>
		public Manifest Info { get; }

	    /// <summary>
	    ///		The files and directories contained by the mod on disk.
	    /// </summary>
		public IDirectoryHandle Resources { get; }

	    /// <summary>
	    ///		The config file available to the mod.
	    /// </summary>
		public ConfigFile Config { get; }

	    /// <summary>
	    ///		The logger available to the mod.
	    /// </summary>
		public ManualLogSource Logger { get; }

	    /// <summary>
	    ///		Creates an instance of <see cref="Mod"/>
	    /// </summary>
	    /// <param name="info"></param>
	    /// <param name="resources"></param>
		public Mod(Manifest info, IDirectoryHandle resources)
		{
			Info = info;
			Resources = resources;
			Config = new ConfigFile(DeliConstants.Filesystem.ConfigsDirectory + "/" + info.Guid + ".cfg", false);
			Logger = BepInEx.Logging.Logger.CreateLogSource(info.Name ?? info.Guid);
		}

		public override string ToString()
		{
			return Info.ToString();
		}

		public class Manifest
		{
			private static readonly Regex _guidFilter = new Regex(@"^[a-z0-9\._]+$");

#pragma warning disable CS8618
			/// <summary>
			///		The globally unique identifier (GUID, crazy) of the mod. This should never be identical to another mod.
			/// </summary>
			[JsonProperty(Required = Required.Always)]
			public string Guid { get; }
			/// <summary>
			///		The version of the mod.
			/// </summary>
			[JsonProperty(Required = Required.Always)]
			public Version Version { get; }
#pragma warning restore CS8618

			/// <summary>
			///		The human-readable name of the mod.
			/// </summary>
			public string? Name { get; }
			/// <summary>
			///		An explanation of what the mod does.
			/// </summary>
			public string? Description { get; }
			/// <summary>
			///		The path to an icon file in the VFS.
			/// </summary>
			public string? IconPath { get; }
			/// <summary>
			///		The URL that this mod originated from, for use in checking the latest version.
			/// </summary>
			public string? SourceUrl { get; }

			/// <summary>
			///		The mods that this mod requires.
			/// </summary>
			public Dictionary<string, Version>? Dependencies { get; }
			/// <summary>
			///		The assets to load during the patcher stage.
			/// </summary>
			public Dictionary<string, AssetLoaderID>? Patchers { get; }
			/// <summary>
			///		The assets to load during the setup stage.
			/// </summary>
			public Dictionary<string, AssetLoaderID>? Setup { get; }
			/// <summary>
			///		The assets to load during runtime stage.
			/// </summary>
			public Dictionary<string, AssetLoaderID>? Runtime { get; }

			// I hate this but we need to programmatically use this.
			[JsonConstructor]
			public Manifest(string guid, Version version, string? name = null, string? description = null, string? iconPath = null, string? sourceUrl = null,
				Dictionary<string, Version>? dependencies = null, Dictionary<string, AssetLoaderID>? patchers = null, Dictionary<string, AssetLoaderID>? setup = null,
				Dictionary<string, AssetLoaderID>? runtime = null)
			{
				Guid = guid;
				Version = version;
				Name = name;
				Description = description;
				IconPath = iconPath;
				SourceUrl = sourceUrl;
				Dependencies = dependencies;
				Patchers = patchers;
				Setup = setup;
				Runtime = runtime;
			}

			[OnDeserialized]
			private void Validate(StreamingContext _)
			{
				// Make sure GUID is normalized
				if (!_guidFilter.IsMatch(Guid))
				{
					throw new FormatException("GUID should be lowercase alphanumeric, with '.' allowed.");
				}
			}

			public override string ToString()
			{
				return $"[{Name ?? Guid} {Version}]";
			}
		}
    }
}