using System.Collections.Generic;

namespace Lime
{
	/// <summary>
	/// ������� ���������. ������������ ����� ���������, ������� ����� ����������� �������, �������� �� �������� � Z-Order
	/// </summary>
	public class RenderChain
	{
		int currentLayer;
		int maxUsedLayer;

		readonly Node[] layers = new Node[Widget.MaxLayer + 1];

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
				node.NextToRender = layers[currentLayer];
				layers[currentLayer] = node;
			}
		}

		public int SetCurrentLayer(int layer)
		{
			if (layer > maxUsedLayer) {
				maxUsedLayer = layer;
			}
			int oldLayer = currentLayer;
			currentLayer = layer;
			return oldLayer;
		}

		public void RenderAndClear()
		{
            for (int i = 0; i <= maxUsedLayer; i++) {
                Node node = layers[i];
                while (node != null) {
                    node.Render();
                    Node next = node.NextToRender;
                    node.NextToRender = null;
                    node = next;
                }
                layers[i] = null;
            }
            maxUsedLayer = 0;
		}
        
		/// <summary>
		/// ����������� ��� ������� � ��� �������, � ����� ��� ������ ������������
		/// </summary>
		public IEnumerable<Node> Enumerate()
		{
			for (int i = 0; i <= maxUsedLayer; i++) {
				for (var node = layers[i]; node != null; node = node.NextToRender) {
					yield return node;
				}
			}
		}
	}
}