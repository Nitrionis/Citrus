using System;
using System.Collections.Generic;
using System.Linq;
using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	public class MaterialAlias { }

	public class MaterialNull : MaterialAlias { }

	/// <summary>
	/// Alias for frame buffers cleaning commands.
	/// </summary>
	public class ClearMaterial : MaterialAlias { }

	public class MaterialsTable
	{
		[YuzuRequired]
		private readonly Dictionary<Type, uint> materialsToIndices;

		[YuzuRequired]
		private readonly Dictionary<uint, Type> indicesToMaterials;

		public MaterialsTable()
		{
			var materialType = typeof(IMaterial);
			var aliasType = typeof(MaterialAlias);
			var types = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(s => s.GetTypes())
				.Where(t => materialType.IsAssignableFrom(t) || aliasType.IsAssignableFrom(t));
			int typesCount = types.Count();
			materialsToIndices = new Dictionary<Type, uint>(typesCount);
			indicesToMaterials = new Dictionary<uint, Type>(typesCount);
			uint materialTypeIndex = 0;
			foreach (var t in types) {
				materialsToIndices.Add(t, materialTypeIndex);
				indicesToMaterials.Add(materialTypeIndex, t);
				materialTypeIndex++;
			}
		}

		public uint GetIndex(Type material) => materialsToIndices[material];

		public Type GetType(uint index) => indicesToMaterials[index];
	}

	public static class NativeMaterialsTable
	{
		public static readonly MaterialsTable Instance = new MaterialsTable();

		public static uint GetIndex(Type material) => Instance.GetIndex(material);

		public static Type GetType(uint index) => Instance.GetType(index);
	}
}
