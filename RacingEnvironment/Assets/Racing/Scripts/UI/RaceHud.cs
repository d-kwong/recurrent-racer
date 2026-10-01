using UnityEngine;
namespace Racing
{
    public sealed class RaceHud : MonoBehaviour
    {
        public RaceEpisode episode;
        public bool showLapHistory;
        Vector2 scroll;
        static string FormatTime(float seconds) => $"{(int)(seconds / 60):00}:{seconds % 60:00.00}";
        Vector2 MapPoint(Vector3 position, Rect rect)
        {
            var ext = episode.track.mapHalfExtent;
            return new Vector2(rect.center.x + position.x / ext.x * rect.width * 0.47f, rect.center.y - position.z / ext.y * rect.height * 0.47f);
        }
        static void Line(Vector2 a, Vector2 b, Color color, float thickness)
        {
            Matrix4x4 old = GUI.matrix; Color oldColor = GUI.color;
            GUI.color = color; GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness / 2, Vector2.Distance(a, b), thickness), Texture2D.whiteTexture);
            GUI.matrix = old; GUI.color = oldColor;
        }
        void OnGUI()
        {
            GUI.Box(new Rect(Screen.width / 2 - 65, Screen.height - 42, 130, 30), $"{episode.vehicle.SpeedKmh:0} km/h");
            if (GUI.Button(new Rect(16, 16, 230, 32), $"Lap {episode.LapNumber}   {FormatTime(episode.CurrentLapTime)}   ▾")) showLapHistory = !showLapHistory;
            if (showLapHistory)
            {
                GUI.Box(new Rect(16, 50, 230, 180), "");
                scroll = GUI.BeginScrollView(new Rect(20, 54, 222, 172), scroll, new Rect(0, 0, 200, Mathf.Max(160, episode.LapTimes.Count * 25)));
                if (episode.LapTimes.Count == 0) GUI.Label(new Rect(5, 0, 195, 25), "No completed laps yet");
                for (int i = 0; i < episode.LapTimes.Count; i++) GUI.Label(new Rect(5, i * 25, 195, 25), $"Lap {i + 1}   {FormatTime(episode.LapTimes[i])}");
                GUI.EndScrollView();
            }
            Rect map = new Rect(Screen.width - 206, 16, 190, 190);
            GUI.Box(map, "N ↑");
            var points = episode.track.centerline;
            for (int i = 0; i < points.Length; i++) Line(MapPoint(points[i], map), MapPoint(points[(i + 1) % points.Length], map), Color.gray, 7);
            Vector2 p = MapPoint(episode.vehicle.transform.position, map);
            Vector3 forward = episode.vehicle.transform.forward;
            Vector2 d = new Vector2(forward.x, -forward.z).normalized;
            Vector2 side = new Vector2(-d.y, d.x);
            Line(p + d * 7, p - d * 5 + side * 5, Color.red, 3);
            Line(p + d * 7, p - d * 5 - side * 5, Color.red, 3);
            if (episode.vehicle.Crashed) GUI.Box(new Rect(Screen.width / 2 - 140, Screen.height / 2 - 25, 280, 50), "CRASH — run ended\nPress R to reset");
        }
    }
}
