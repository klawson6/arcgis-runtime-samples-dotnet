using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;

namespace ArcGISRuntime.Samples.Scene.GetElevationGraph
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Get elevation graph between two points.",
        category: "Scene",
        description: "Generate an elevation graph showing the change in eleevation between two points.",
        instructions: "Tap on any two points on the surface.",
        tags: new[] { "elevation", "point", "surface" })]
    public partial class GetElevationGraph
    {
        // URL of the elevation service - provides elevation component of the scene.
        private readonly Uri _elevationUri = new Uri("https://elevation3d.arcgis.com/arcgis/rest/services/WorldElevation3D/Terrain3D/ImageServer");

        // Starting point of the observer.
        private readonly MapPoint _observerPoint = new MapPoint(83.9, 28.42, SpatialReferences.Wgs84);

        // Graphics overlay.
        private GraphicsOverlay _overlay;

        // Surface (for elevation).
        private Surface _baseSurface;

        // Create symbols for the text and marker.
        private ElevationPoint _elevationMarkerFrom;
        private ElevationPoint _elevationMarkerTo;

        public GetElevationGraph()
        {
            InitializeComponent();
            // Create the UI, setup the control references and execute initialization.
            Initialize();

            // Handle taps on the scene view for getting elevation.
            MySceneView.GeoViewTapped += SceneViewTapped;
        }
        private void Initialize()
        {
            // Create the camera for the scene.
            Camera camera = new Camera(_observerPoint, 20000.0, 10.0, 70.0, 0.0);

            // Create a scene.
            Esri.ArcGISRuntime.Mapping.Scene myScene = new Esri.ArcGISRuntime.Mapping.Scene(Basemap.CreateImageryWithLabels())
            {
                // Set the initial viewpoint.
                InitialViewpoint = new Viewpoint(_observerPoint, 1000000, camera)
            };

            // Create the marker for showing where the user taps.
            _elevationMarkerFrom = new ElevationPoint(Color.Red);
            _elevationMarkerTo = new ElevationPoint(Color.Blue);

            // Create the base surface.
            _baseSurface = new Surface();
            _baseSurface.ElevationSources.Add(new ArcGISTiledElevationSource(_elevationUri));

            // Add the base surface to the scene.
            myScene.BaseSurface = _baseSurface;

            // Graphics overlay for displaying points.
            _overlay = new GraphicsOverlay
            {
                SceneProperties = new LayerSceneProperties(SurfacePlacement.Absolute)
            };
            MySceneView.GraphicsOverlays.Add(_overlay);

            // Add the scene to the view.
            MySceneView.Scene = myScene;
        }

        private async void SceneViewTapped(object sender, Esri.ArcGISRuntime.UI.Controls.GeoViewInputEventArgs e)
        {
            try
            {
                // Remove this method from the event handler to prevent concurrent calls.
                MySceneView.GeoViewTapped -= SceneViewTapped;

                // Check that the point is on the surface.
                if (e.Location != null)
                {
                    if(_elevationMarkerFrom.isSet() && _elevationMarkerTo.isSet())
                    {
                        _overlay.Graphics.Clear();
                        _elevationMarkerFrom = new ElevationPoint(Color.Red);
                        _elevationMarkerTo = new ElevationPoint(Color.Blue);
                    }
                    if (!_elevationMarkerFrom.isSet())
                    {
                        setPoint(_elevationMarkerFrom, e.Location);
                    } else
                    {
                        setPoint(_elevationMarkerTo, e.Location);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                MessageBox.Show(ex.Message, "Sample error");
            }
            finally
            {
                // Re-add to the event handler.
                MySceneView.GeoViewTapped += SceneViewTapped;
            }
        }
        private async void setPoint(ElevationPoint p, MapPoint location)
        {
            // Get the elevation value.
            double elevation = await _baseSurface.GetElevationAsync(location);
            // Set the text displaying the elevation.
            p.setContent($"{Math.Round(elevation)} m", location);

            // Add the text to the graphics overlay.
            _overlay.Graphics.Add(p.getTextGraphic());
            _overlay.Graphics.Add(p.getMarkerGraphic());
        }
        class ElevationPoint
        {
            // Create symbols for the text and marker.
            private SimpleMarkerSceneSymbol _elevationMarker;
            private TextSymbol _elevationTextSymbol;
            private readonly Graphic _elevationTextGraphic = new Graphic();
            private readonly Graphic _elevationMarkerGraphic = new Graphic();
            private MapPoint _elevationGeometry;
            private bool set;

            public ElevationPoint()
            {
                // Create the marker for showing where the user taps.
                _elevationMarker = SimpleMarkerSceneSymbol.CreateCylinder(Color.Yellow, 10, 750);

                // Create the text for displaying the elevation value.
                _elevationTextSymbol = new TextSymbol("", Color.Yellow, 20, Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center, Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle);
                _elevationTextGraphic.Symbol = _elevationTextSymbol;
                _elevationMarkerGraphic.Symbol = _elevationMarker;
                _elevationGeometry = null;

                set = false;
            }
            public ElevationPoint(Color c)
            {
                // Create the marker for showing where the user taps.
                _elevationMarker = SimpleMarkerSceneSymbol.CreateCylinder(c, 10, 750);

                // Create the text for displaying the elevation value.
                _elevationTextSymbol = new TextSymbol("", c, 20, Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center, Esri.ArcGISRuntime.Symbology.VerticalAlignment.Middle);
                _elevationTextGraphic.Symbol = _elevationTextSymbol;
                _elevationMarkerGraphic.Symbol = _elevationMarker;
                _elevationGeometry = null;

                set = false;
            }

            public bool isSet() { return set; }
            public String getText()
            {
                return _elevationTextSymbol.Text;
            }
            public void setText(String text)
            {
                _elevationTextSymbol.Text = text;
            }
            public MapPoint getGeometry()
            {
                return _elevationGeometry;
            }
            public void setGeometry(MapPoint location)
            {
                _elevationTextGraphic.Geometry = new MapPoint(location.X, location.Y, location.Z + 850);
                _elevationMarkerGraphic.Geometry = new MapPoint(location.X, location.Y, location.Z);
                _elevationGeometry = location;
            }
            public Graphic getTextGraphic()
            {
                return _elevationTextGraphic;
            }
            public Graphic getMarkerGraphic()
            {
                return _elevationMarkerGraphic;
            }
            public void setAll(ElevationPoint e)
            {
                setText(e.getText());
                if (e.getGeometry() != null)
                    setGeometry(e.getGeometry());
            }
            public void setContent(String text, MapPoint location)
            {
                setText(text);
                setGeometry(location);
                set = true;
            }
        }
    }


}
