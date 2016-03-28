using System.Collections.Generic;

namespace Lime
{
	/// <summary>
	/// ������� ���������. ������������ ����� ���������, ������� ����� ����������� �������, �������� �� �������� � Z-Order
	/// </summary>
	public class RenderChain
	{
		private int currentLayer;
		private IHitProcessor hitProcessor;

		public int MaxUsedLayer { get; private set; }

		public readonly Node[] Layers = new Node[Widget.MaxLayer + 1];

		public RenderChain(IHitProcessor hitProcessor = null)
		{
			if (hitProcessor == null) {
				hitProcessor = DefaultHitProcessor.Instance;
			}
			this.hitProcessor = hitProcessor;
		}
		/// <summary>
		/// ��������� ������ � ��� ��� �������� ������� � ������� ���������
		/// </summary>
		/// <param name="node">����������� ������</param>
		/// <param name="layer">����. ������ ������� ���������. ��� ������ ��������, ��� ������� ����� ��������� ������. 0 - �������� �� ���������. �� 0 �� 99</param>
		public void Add(Node node, int layer = 0)
		{
			if (layer != 0) {
				int oldLayer = SetCurrentLayer(layer);
				Add(node, 0);
				SetCurrentLayer(oldLayer);
			} else {
				node.NextToRender = Layers[currentLayer];
				Layers[currentLayer] = node;
			}
		}

		public int SetCurrentLayer(int layer)
		{
			if (layer > MaxUsedLayer) {
				MaxUsedLayer = layer;
			}
			int oldLayer = currentLayer;
			currentLayer = layer;
			return oldLayer;
		}

		public void RenderAndClear()
		{
			for (int i = 0; i <= MaxUsedLayer; i++) {
				Node node = Layers[i];
				while (node != null) {
					hitProcessor.PerformHitTest(node);
					node.Render();
					Node next = node.NextToRender;
					node.NextToRender = null;
					node = next;
				}
				Layers[i] = null;
			}
			MaxUsedLayer = 0;
		}

		public void Clear()
		{
			for (int i = 0; i <= MaxUsedLayer; i++) {
				Node node = Layers[i];
				while (node != null) {
					Node next = node.NextToRender;
					node.NextToRender = null;
					node = next;
				}
				Layers[i] = null;
			}
			MaxUsedLayer = 0;
		}

		/// <summary>
		/// ����������� ��� ������� � ��� �������, � ����� ��� ������ ������������
		/// </summary>
		public IEnumerable<Node> Enumerate()
		{
			for (int i = 0; i <= MaxUsedLayer; i++) {
				for (var node = Layers[i]; node != null; node = node.NextToRender) {
					yield return node;
				}
			}
		}
	}
}