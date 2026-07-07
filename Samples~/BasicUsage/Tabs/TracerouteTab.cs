using System;
using System.Text;
using UnityEngine;
using RatatuiUnity;

namespace RatatuiUnity.Demo
{
    /// <summary>
    /// Demo2 "Traceroute" tab — hop table, per-hop ping sparklines, Turkey region canvas.
    /// Route: Ankara → İstanbul. All ping data is generated in C#.
    /// </summary>
    public class TracerouteTab : ITab
    {
        public string Title => "Traceroute";

        private struct Hop
        {
            public int    Ttl;
            public string Address;
            public string Host;
            public float  LatMs;
            public float  Lon; // x canvas (longitude)
            public float  Lat; // y canvas (latitude)
        }

        // Route: Ankara → İstanbul (7 hops)
        private static readonly Hop[] Hops =
        {
            new Hop { Ttl=1, Address="10.0.0.1",      Host="gateway.local",       LatMs= 1f, Lon=32.87f, Lat=39.93f }, // Ankara
            new Hop { Ttl=2, Address="195.175.39.1",  Host="ank-core.turk.net",   LatMs= 4f, Lon=32.50f, Lat=40.10f },
            new Hop { Ttl=3, Address="195.175.39.10", Host="ank-bb1.turk.net",    LatMs= 7f, Lon=31.80f, Lat=40.30f },
            new Hop { Ttl=4, Address="195.175.40.1",  Host="ank-ist.turk.net",    LatMs=12f, Lon=30.90f, Lat=40.60f },
            new Hop { Ttl=5, Address="195.175.41.5",  Host="ist-entry.turk.net",  LatMs=16f, Lon=29.80f, Lat=40.90f },
            new Hop { Ttl=6, Address="195.175.42.1",  Host="ist-core.turk.net",   LatMs=19f, Lon=29.20f, Lat=41.10f },
            new Hop { Ttl=7, Address="185.60.240.1",  Host="destination.ist",     LatMs=22f, Lon=28.97f, Lat=41.01f }, // İstanbul
        };

        // Rolling ping per hop
        private readonly ulong[][] _pings = new ulong[7][];
        private readonly System.Random _rng = new System.Random(99);
        private float _tickTimer;

        public TracerouteTab()
        {
            for (int i = 0; i < _pings.Length; i++)
                _pings[i] = new ulong[30];
        }

        public void Update(float dt)
        {
            _tickTimer += dt;
            if (_tickTimer < 0.3f) return;
            _tickTimer -= 0.3f;

            for (int i = 0; i < _pings.Length; i++)
            {
                Array.Copy(_pings[i], 1, _pings[i], 0, _pings[i].Length - 1);
                ulong jitter = (ulong)_rng.Next(0, 8);
                _pings[i][_pings[i].Length - 1] = (ulong)Hops[i].LatMs + jitter;
            }
        }

        public void OnInput(KeyCode key) { }
        public void OnKeyEvent(TerminalKeyEvent e) { }
        public void OnMouseEvent(TerminalMouseEvent e) { }
        public void OnHoverChanged(TerminalHoverState oldState, TerminalHoverState newState) { }

        public void Render(RatatuiTerminal term, uint area)
        {
            var cols = term.Split(area, Direction.Horizontal,
                Constraint.Percentage(55),
                Constraint.Percentage(45));

            if (cols.Length < 2) return;

            RenderHopTable(term, cols[0]);
            RenderMap(term, cols[1]);
        }

        private void RenderHopTable(RatatuiTerminal term, uint area)
        {
            var rows = term.Split(area, Direction.Vertical,
                Constraint.Percentage(50),
                Constraint.Percentage(50));

            if (rows.Length < 2) return;

            // Hop table
            term.Block(rows[0], "Hops", Borders.All);
            uint tableInner = term.Inner(rows[0]);
            var sb = new StringBuilder("TTL\tAddress\tHost\tms\n");
            foreach (var h in Hops)
                sb.AppendLine($"{h.Ttl}\t{h.Address}\t{h.Host}\t{h.LatMs:F0}");
            term.Table(tableInner, sb.ToString().TrimEnd());

            // Sparklines per hop
            term.Block(rows[1], "Latency", Borders.All);
            uint sparkInner = term.Inner(rows[1]);
            var sparkRows = term.Split(sparkInner, Direction.Vertical,
                Constraint.Length(1), Constraint.Length(1), Constraint.Length(1),
                Constraint.Length(1), Constraint.Length(1), Constraint.Length(1),
                Constraint.Length(1));

            for (int i = 0; i < Math.Min(_pings.Length, sparkRows.Length); i++)
            {
                term.SetStyle(Color.cyan, Color.clear);
                term.Sparkline(sparkRows[i], _pings[i]);
            }
        }

        // ── Türkiye dış sınırı — TEK KAPALI POLİGON ─────────────────────────────
        //
        // Saat yönünde: Boğaz/Karadeniz → doğu → Akdeniz → Ege (yarımadalarla) →
        // Çanakkale Boğazı geçişi → Gelibolu → Saros → Trakya → İstanbul → kapalı.
        //
        // Çanakkale Boğazı yaklaşık 0.15–0.2° enleminde — bu ölçekte piksel altı,
        // bu yüzden Asya kıyısından Avrupa kıyısına geçiş doğrudan çizgi olarak yapılıyor.
        private static readonly (double Lon, double Lat)[] TurkeyOutline =
        {
            // ── Boğaz/Karadeniz başlangıç ─────────────────────────────────────
            (29.0, 41.2),

            // ── Karadeniz kıyısı (B → D) ──────────────────────────────────────
            (29.5, 41.2), (30.3, 41.2), (31.0, 41.5),
            (31.8, 41.5), (33.0, 41.9), (35.0, 42.1),
            (37.0, 41.5), (37.5, 41.0), (37.8, 40.8),

            // ── Canvas doğu kenarı (güneye) ────────────────────────────────────
            (37.8, 36.8),

            // ── Akdeniz kıyısı (D → B) ────────────────────────────────────────
            (37.5, 36.7), (37.0, 36.5), (36.2, 36.2),
            (35.5, 36.2), (35.0, 36.3), (33.5, 36.5),
            (32.0, 36.5), (30.5, 36.5), (29.5, 36.7),
            (29.0, 37.0), (28.5, 37.0),

            // ── Ege kıyısı (G → K, yarımada detaylarıyla) ─────────────────────
            // Datça – Marmaris
            (28.0, 36.9), (27.7, 36.9), (27.4, 37.0),
            // Bodrum Yarımadası (batıya uzanır)
            (27.2, 37.1), (27.0, 37.25), (27.3, 37.4), (27.5, 37.5),
            // Güllük Körfezi
            (27.5, 37.7), (27.3, 37.8),
            // Kuşadası
            (27.1, 37.8), (26.9, 37.9), (26.7, 38.0),
            // Çeşme Yarımadası (batıya çıkıntı)
            (26.5, 38.0), (26.3, 38.25), (26.5, 38.3),
            // Karaburun Yarımadası (daha da batıya)
            (26.5, 38.45), (26.4, 38.65), (26.6, 38.8),
            // Dikili / Çandarlı Körfezi
            (26.7, 38.9), (26.95, 39.0),
            (26.9, 39.2), (27.1, 39.35), (26.9, 39.5),
            // Edremit Körfezi (doğuya girer, geri döner)
            (27.5, 39.6), (27.2, 39.7), (26.9, 39.85),
            // Biga Yarımadası
            (26.7, 39.95), (26.6, 40.1), (26.5, 40.3),
            // Çanakkale Boğazı — Asya kıyısı güney girişi
            (26.45, 40.1), (26.4, 40.0),

            // ── ÇANAKKALE BOĞAZI GEÇİŞİ (Asya → Avrupa) ─────────────────────
            // Boğaz bu ölçekte piksel altı — kısa bir "köprü" çizgisi
            (26.2, 40.05),   // Seddülbahir (Gelibolu güney ucu)

            // ── Gelibolu Yarımadası — batı kıyısı (Saros tarafı, K'ye) ────────
            (26.1, 40.15), (26.2, 40.3), (26.3, 40.45),
            (26.45, 40.6),  // Gelibolu kuzeyi
            (26.6, 40.7),   // Saros Körfezi ile kavşak

            // ── Saros Körfezi kuzey kıyısı (Trakya güney kıyısı, D'ye) ────────
            (26.75, 40.8), (27.0, 40.9), (27.5, 40.97),

            // ── Trakya Marmara kıyısı (D'ye, İstanbul'a) ─────────────────────
            (28.0, 41.0), (28.5, 41.05), (28.9, 41.05),

            // ── İstanbul / Boğaz kapanış ──────────────────────────────────────
            (29.0, 41.15), (29.0, 41.2),
        };

        // ── Güney Marmara kıyısı (Asya yakası) ───────────────────────────────────
        // Bu ayrı çizgi, Trakya ile Anadolu arasındaki Marmara Denizi'ni görünür kılar.
        // Çanakkale Boğazı kuzey çıkışı → Bandırma → Mudanya → Yalova → Asya İstanbul
        private static readonly (double Lon, double Lat)[] MarmaraSouth =
        {
            (26.8, 40.2),                   // Çanakkale, Asya yakası
            (27.0, 40.3), (27.4, 40.38),    // Erdek yönü
            (27.9, 40.4), (28.1, 40.4),     // Bandırma/Erdek
            (28.5, 40.42), (28.9, 40.45),   // Mudanya/Bursa kıyısı
            (29.1, 40.65), (29.3, 40.78),   // Yalova bölgesi
            (29.1, 41.0),                   // Asya İstanbul (Üsküdar/Kadıköy)
        };

        // ── Trakya kuzey sınırı (Bulgaristan/Yunanistan sınırı) ──────────────────
        // Trakya'nın kuzey/batı kara sınırı — kapalı yarımada görünümü için
        private static readonly (double Lon, double Lat)[] ThraceNorth =
        {
            (28.9, 41.05),  // Avrupa İstanbul / Boğaz
            (28.0, 41.6),   // İstanbul kuzeyi (Bulgaristan sınırı yönü)
            (27.0, 41.8),
            (26.6, 41.6),
            (26.35, 41.2),  // Yunanistan/Bulgaristan sınırı köşesi
            (26.1, 40.9),   // İpsala / Meriç nehri (batı sınır)
            (26.1, 40.7),   // Güney Trakya – Saros Körfezi'ne iniş
            (26.2, 40.4),
            (26.2, 40.05),  // Gelibolu güney ucu (Seddülbahir)
        };

        // ── Helper ────────────────────────────────────────────────────────────────

        private static void DrawPolyline(CanvasBuilder canvas,
            (double Lon, double Lat)[] pts, Color color)
        {
            for (int i = 0; i < pts.Length - 1; i++)
                canvas.Line(pts[i].Lon, pts[i].Lat,
                            pts[i + 1].Lon, pts[i + 1].Lat, color);
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void RenderMap(RatatuiTerminal term, uint area)
        {
            const double xMin = 24.0, xMax = 38.0;  // 14° boylam
            const double yMin = 36.0, yMax = 44.0;  //  8° enlem

            term.Block(area, "Route (TR): Ankara -> Istanbul", Borders.All);
            uint inner = term.Inner(area);

            var canvas = term.BeginCanvas(inner, xMin, xMax, yMin, yMax, Marker.Braille);

            var coast = new Color(0.65f, 0.65f, 0.65f);

            // 1a. Türkiye dış sınırı (Asya ana kıta + Karadeniz + Akdeniz + Ege + Gelibolu hattı)
            DrawPolyline(canvas, TurkeyOutline, coast);

            // 1b. Güney Marmara kıyısı — Marmara Denizi'ni ve Trakya'yı görünür yapar
            DrawPolyline(canvas, MarmaraSouth, coast);

            // 1c. Trakya kuzey/batı kara sınırı — kapalı yarımada görünümü
            DrawPolyline(canvas, ThraceNorth, coast);

            canvas.Layer();

            // 2. Hop rotası (sarı)
            for (int i = 0; i < Hops.Length - 1; i++)
                canvas.Line(Hops[i].Lon, Hops[i].Lat,
                            Hops[i + 1].Lon, Hops[i + 1].Lat, Color.yellow);

            // 3. Ara hop'lar (cyan)
            for (int i = 1; i < Hops.Length - 1; i++)
                canvas.Points(new double[] { Hops[i].Lon, Hops[i].Lat }, Color.cyan);

            // 4. Ankara (yeşil, kaynak)
            canvas.Points(new double[] { Hops[0].Lon, Hops[0].Lat }, Color.green);
            canvas.Text(Hops[0].Lon + 0.2, Hops[0].Lat - 0.7, "Ankara", Color.green);

            // 5. İstanbul (kırmızı, hedef)
            int last = Hops.Length - 1;
            canvas.Points(new double[] { Hops[last].Lon, Hops[last].Lat }, Color.red);
            canvas.Text(Hops[last].Lon + 0.2, Hops[last].Lat + 0.5, "Istanbul", Color.red);

            canvas.Render();
        }
    }
}
