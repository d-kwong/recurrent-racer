using System;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Racing.Editor
{
    public static class CompactPresentationVerification
    {
        static void Check(bool ok,string message) {if(!ok) throw new InvalidOperationException("Compact presentation: "+message);}
        public static void Run()
        {
            EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);
            var episode=UnityEngine.Object.FindFirstObjectByType<RaceEpisode>();var track=episode.track;
            var generator=track.GetComponent<ProceduralTrack>();if(generator==null) generator=track.gameObject.AddComponent<ProceduralTrack>();
            foreach(int seed in new[]{50000,50001,50002})
            {
                generator.Configure(track,episode,1,seed,new TrackParameters(),layout:1);
                var root=track.transform.Find("Procedural geometry");int red=0,white=0;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if(!renderer.name.Contains("curb")) continue;
                    int tile=int.Parse(renderer.name.Substring(renderer.name.LastIndexOf(' ')+1));
                    bool isWhite=renderer.sharedMaterial.name=="Generated curb white";
                    Check(isWhite==(tile%2==1),"stripe parity seed="+seed);
                    Check(renderer.GetComponent<Collider>()==null,"curb render only");
                    if(isWhite)white++;else {red++;var color=renderer.sharedMaterial.color;Check(color.r>color.g*2&&color.r>color.b*2,"red base preserved");}
                }
                Check(red>0&&white>0,"both colors");
            }
            Debug.Log("COMPACT_PRESENTATION_VERIFICATION_SUCCESS: sequential seeds 50000/50001/50002 curb parity and renderer-only strips");
        }
    }
}
