#if PROFILER

using System;
using Lime;
using Tangerine.UI.Charts;

namespace Tangerine.UI
{
	using LegendItemDescription = ChartsLegend.ItemDescription;
	
	internal class ChartsPanel : Widget
	{
		public ChartsPanel(Func<Image> SearchIconBuilder, out ChartVisibilityControllers visibilityControllers)
		{
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;
			FixedHorizontalSpacingCharts.Parameters GetParameters(int chartsCount) => 
				new FixedHorizontalSpacingCharts.Parameters {
					ControlPointsCount = 160,
					ChartsCount = chartsCount,
					ControlPointsSpacing = 5
				};
			Widget CreateLegendWrapper(string name, ChartsLegend chartsLegend) => new Widget {
				Layout = new VBoxLayout(),
				Padding = new Thickness(0, 8, 4, 4),
				Nodes = {
					new ThemedSimpleText("Main Thread"),
					chartsLegend,
				}
			};
			LegendItemDescription CreateFloatLegendItem(string name) =>
				new LegendItemDescription {
					Label = name,
					ValueFormat = "0.##"
				};
			LegendItemDescription CreateIntLegendItem(string name) =>
				new LegendItemDescription {
					Label = name,
					ValueFormat = "0."
				};
			(Widget, ChartsLegend, LineCharts) CreateLineCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new LineCharts(GetParameters(items.Length));
				var chartsLegend = new ChartsLegend(new [] { chartsGroup }, items);
				return (CreateLegendWrapper(name, chartsLegend), chartsLegend, chartsGroup);
			}
			(Widget, ChartsLegend, StackedAreaCharts) CreateAreaCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new StackedAreaCharts(GetParameters(items.Length));
				var chartsLegend = new ChartsLegend(new [] { chartsGroup }, items);
				return (CreateLegendWrapper(name, chartsLegend), chartsLegend, chartsGroup);
			}
			var (updateChartsLegendWrapper, updateChartsLegend, updateCharts) = 
				CreateAreaCharts("Main Thread", new[] {
					CreateFloatLegendItem("Update"),
					CreateFloatLegendItem("Selected")
				});
			var (renderChartsLegendWrapper, renderChartsLegend, renderCharts) = 
				CreateAreaCharts("Render Thread",new[] {
					CreateFloatLegendItem("Render"),
					CreateFloatLegendItem("Selected")
				});
			var (gpuChartsLegendWrapper, gpuChartsLegend, gpuCharts) = 
				CreateAreaCharts("GPU",new[] {
					CreateFloatLegendItem("Drawing"),
					CreateFloatLegendItem("Selected")
				});
			var geometryChartsDescriptions = new [] {
				CreateIntLegendItem("Saved By Batching"),
				CreateIntLegendItem("Draw Calls"),
				CreateIntLegendItem("Vertices"),
				CreateIntLegendItem("Triangles"),
			};
			var (sceneGeometryChartsLegendWrapper, sceneGeometryChartsLegend, sceneGeometryCharts) = 
				CreateLineCharts("Scene Geometry", geometryChartsDescriptions);
			var (fullGeometryChartsLegendWrapper, fullGeometryChartsLegend, fullGeometryCharts) = 
				CreateLineCharts("Full Geometry", geometryChartsDescriptions);
			var (gcChartsLegendWrapper, gcChartsLegend, gcCharts) = 
				CreateLineCharts("Garbage Collector",new[] {
					CreateFloatLegendItem("Memory"),
					CreateIntLegendItem("GC 0"),
					CreateIntLegendItem("GC 1"),
					CreateIntLegendItem("GC 2"),
					CreateIntLegendItem("GC 3"),
				});
			void SetMinMaxHeight(int value, Widget legend, Widget charts) {
				legend.MinMaxHeight = value;
				charts.MinMaxHeight = value;
			}
			SetMinMaxHeight(48, updateChartsLegendWrapper, updateCharts);
			SetMinMaxHeight(48, renderChartsLegendWrapper, renderCharts);
			SetMinMaxHeight(48, gpuChartsLegendWrapper, gpuCharts);
			SetMinMaxHeight(64, sceneGeometryChartsLegendWrapper, sceneGeometryCharts);
			SetMinMaxHeight(64, fullGeometryChartsLegendWrapper, fullGeometryCharts);
			SetMinMaxHeight(72, gcChartsLegendWrapper, gcCharts);
			Nodes.Add(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout(),
						Nodes = {
							updateChartsLegendWrapper,
							renderChartsLegendWrapper,
							gpuChartsLegendWrapper,
							sceneGeometryChartsLegendWrapper,
							fullGeometryChartsLegendWrapper,
							gcChartsLegendWrapper
						}
					},
					new Widget {
						Layout = new VBoxLayout(),
						Nodes = {
							updateCharts,
							renderCharts,
							gpuCharts,
							sceneGeometryCharts,
							fullGeometryCharts,
							gcCharts
						}
					}
				}
			});
			visibilityControllers = new ChartVisibilityControllers {
				MainThreadChartsSetVisible = value => { 
					updateChartsLegendWrapper.Visible = value;
					updateCharts.Visible = value;
				},
				RenderThreadChartsSetVisible = value => { 
					renderChartsLegendWrapper.Visible = value;
					renderCharts.Visible = value;
				},
				GpuChartsSetVisible = value => { 
					gpuChartsLegendWrapper.Visible = value;
					gpuCharts.Visible = value;
				},
				SceneGeometryChartsSetVisible = value => { 
					sceneGeometryChartsLegendWrapper.Visible = value;
					sceneGeometryCharts.Visible = value;
				},
				FullGeometryChartsSetVisible = value => { 
					fullGeometryChartsLegendWrapper.Visible = value;
					fullGeometryCharts.Visible = value;
				},
				GarbageCollectorChartsSetVisible = value => { 
					gcChartsLegendWrapper.Visible = value;
					gcCharts.Visible = value;
				}
			};
			
		}
	}
}

#endif // PROFILER