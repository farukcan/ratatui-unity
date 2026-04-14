using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo1 "Servers" tab — server table on the left, world-map canvas on the right.
    /// All server coordinates and state are owned in C#.
    /// </summary>
    public class ServersTab : ITab
    {
        public string Title => "Servers";

        private struct Server
        {
            public string Name;
            public string Location;
            public string Status;
            public float Lat;  // y in canvas coords
            public float Lon;  // x in canvas coords
        }

        private static readonly Server[] Servers =
        {
            new Server { Name="NYC",     Location="New York, US",   Status="Up",  Lat= 40.7f, Lon=-74.0f },
            new Server { Name="LON",     Location="London, UK",     Status="Up",  Lat= 51.5f, Lon= -0.1f },
            new Server { Name="TYO",     Location="Tokyo, JP",      Status="Up",  Lat= 35.7f, Lon=139.7f },
            new Server { Name="SYD",     Location="Sydney, AU",     Status="Up",  Lat=-33.9f, Lon=151.2f },
            new Server { Name="SAO",     Location="São Paulo, BR",  Status="Down",Lat=-23.5f, Lon=-46.6f },
            new Server { Name="CPT",     Location="Cape Town, ZA",  Status="Up",  Lat=-33.9f, Lon= 18.4f },
        };

        private float _elapsed;

        public void Update(float dt) { _elapsed += dt; }
        public void OnInput(KeyCode key) { }
        public void OnKeyEvent(TerminalKeyEvent e) { }
        public void OnMouseEvent(TerminalMouseEvent e) { }
        public void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            var cols = term.Split(area, Direction.Horizontal,
                Constraint.Percentage(35),
                Constraint.Percentage(65));

            if (cols.Length < 2) return;

            RenderServerTable(term, cols[0]);
            RenderWorldMap(term, cols[1]);
        }

        private void RenderServerTable(RatatuiTerminal term, uint area)
        {
            term.Block(area, "Servers", Borders.All);
            uint inner = term.Inner(area);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Name\tLocation\tStatus");
            foreach (var s in Servers)
                sb.AppendLine($"{s.Name}\t{s.Location}\t{s.Status}");

            term.Table(inner, sb.ToString().TrimEnd());
        }

        private void RenderWorldMap(RatatuiTerminal term, uint area)
        {
            term.Block(area, "World", Borders.All);
            uint inner = term.Inner(area);

            var canvas = term.BeginCanvas(inner, -180, 180, -90, 90, Marker.Braille);
            canvas.Map(MapResolution.High);
            canvas.Layer();

            // Draw connection lines between sequential servers
            for (int i = 0; i < Servers.Length - 1; i++)
                canvas.Line(Servers[i].Lon, Servers[i].Lat,
                            Servers[i + 1].Lon, Servers[i + 1].Lat,
                            new Color(1f, 1f, 0f, 1f));

            // Draw server points + labels
            foreach (var s in Servers)
            {
                Color c = s.Status == "Up"
                    ? new Color(0.2f, 1f, 0.2f)
                    : new Color(1f, 0.2f, 0.2f);
                canvas.Points(new double[] { s.Lon, s.Lat }, c);
                canvas.Text(s.Lon + 1.5, s.Lat + 1.5, s.Name, Color.white);
            }

            canvas.Render();
        }
    }
}
