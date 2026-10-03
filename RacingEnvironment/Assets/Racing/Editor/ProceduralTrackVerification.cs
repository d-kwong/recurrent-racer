using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Racing.Editor
{
    public static class ProceduralTrackVerification
    {
        [Serializable] public sealed class SpawnCoverage
        {
            public int seed, draws=400, originalCount, nearTurnCount, leftCount, rightCount;
            public int[] turnCounts;
            public float nearTurnFraction;
        }
        [Serializable] public sealed class Summary { public int generatedSeeds; public string generatorVersion="open-arcs-v1"; public string[] geometrySha256; public TrackRecord[] representativeTracks; public SpawnCoverage[] spawnCoverage; }
        static void Check(bool valid, string message) { if(!valid) throw new InvalidOperationException("Procedural verification: "+message); }
        public static void Run()
        {
            var parameters=new TrackParameters(); var hashes=new List<string>();
            for(int seed=0;seed<1000;seed++)
            {
                var a=ProceduralTrack.Generate(seed,parameters); var b=ProceduralTrack.Generate(seed,parameters);
                Check(a.geometrySha256==b.geometrySha256,"determinism seed="+seed);
                Check(ProceduralTrack.IsValid(a.centerline,a.cumulativeMetres,15),"route clearance");
                Check(a.taskDistance>50 && a.spawnDistance==4 && a.finishDistance==a.length-6,"cap clearance");
                hashes.Add(a.geometrySha256);
            }
            int[] seeds={101,211,307,1009,2003,3001}; var representative=new List<TrackRecord>(); var coverage=new List<SpawnCoverage>();
            var root=new GameObject("Procedural verification scratch");
            try
            {
                var track=root.AddComponent<RaceTrack>(); track.centerline=new[] {Vector3.zero,Vector3.forward*50,Vector3.right*50};
                var fixedSpawn=new GameObject("Fixed spawn").transform; fixedSpawn.SetParent(root.transform); track.spawn=fixedSpawn;
                var episode=root.AddComponent<RaceEpisode>(); episode.track=track;
                var generator=root.AddComponent<ProceduralTrack>(); var progress=root.AddComponent<RouteProgress>(); progress.track=track;
                foreach(int seed in seeds)
                {
                    generator.Configure(track,episode,1,seed,parameters); var record=generator.Record; representative.Add(record);
                    progress.ResetProgress(); Check(Mathf.Abs(progress.RouteLength-record.length)<.001f,"open length without closing chord");
                    var generated=track.spawn.parent;
                    Check(!fixedSpawn.gameObject.activeSelf,"fixed geometry disabled");
                    Check(generated.GetComponentsInChildren<CheckpointGate>().Length==track.checkpointCount,"gate count");
                    foreach(var collider in generated.GetComponentsInChildren<MeshCollider>())
                    {
                        Check(!collider.isTrigger,"baseline-compatible nontrigger ribbons");
                        Vector3[] expected=collider.name=="Left sensor edge" ? record.leftEdge : collider.name=="Right sensor edge" ? record.rightEdge :
                            collider.name=="Left crash boundary" ? record.leftBoundary : collider.name=="Right crash boundary" ? record.rightBoundary : null;
                        if(expected==null) continue;
                        var vertices=collider.sharedMesh.vertices; Check(vertices.Length==expected.Length*2,"ribbon count");
                        for(int i=0;i<expected.Length;i++) Check(Vector3.Distance(vertices[i*2]+Vector3.up,expected[i])<.0001f,"ribbon alignment");
                    }
                    Physics.SyncTransforms();
                    int sample=record.centerline.Length/2;
                    Vector3 origin=record.centerline[sample]+Vector3.up*.3f;
                    Vector3 normal=Vector3.Cross(Vector3.up,ProceduralTrack.Tangent(record.centerline,sample));
                    Check(Physics.Raycast(origin,normal,out var hit,8,1<<8,QueryTriggerInteraction.Collide),"sensor hit");
                    Check(Mathf.Abs(hit.distance-track.roadWidth/2)<.12f,"sensor distance alignment");
                    Check(Physics.Raycast(origin,normal,out var crashHit,8,1,QueryTriggerInteraction.Ignore) && crashHit.collider.GetComponent<CrashBoundary>()!=null,"crash boundary hit");
                    Check(Mathf.Abs(crashHit.distance-(track.roadWidth/2+track.curbWidth))<.12f,"boundary distance alignment");
                    Check(!progress.ValidateGate(0),"finish cannot precede intermediate gates");
                    for(int gate=1;gate<track.checkpointCount;gate++)
                    {
                        Vector3 position=ProceduralTrack.At(record,record.gateDistances[gate],out _);
                        Check(progress.Advance(position,record.length),"ordered progress");
                        Check(progress.ValidateGate(gate),"ordered gate accepted"); progress.AcceptGate(gate);
                    }
                    Vector3 finish=record.finishPosition;
                    Check(progress.Advance(finish,record.length) && progress.ValidateGate(0),"finish accepted before cap");
                    progress.AcceptGate(0);
                    float before=progress.TravelMetres; progress.Advance(record.centerline[record.centerline.Length-1]+Vector3.forward*100,record.length*2);
                    Check(progress.TravelMetres<=record.length-record.spawnDistance+.001f,"endpoint does not wrap");
                    progress.ResetProgress(); Check(progress.ValidatedGates==0 && progress.TravelMetres==0,"reset gates/progress");
                    Check(!progress.Advance(record.finishPosition,1),"implausible progress rejected");
                    // Same sample count, changed distances: reset must invalidate its length cache.
                    var original=track.centerline; var scaled=(Vector3[])original.Clone();
                    for(int i=0;i<scaled.Length;i++) scaled[i]*=1.01f;
                    track.centerline=scaled; progress.ResetProgress(); Check(progress.RouteLength>record.length+.1f,"same-count length cache rebuilt");
                    track.centerline=original;
                    for(int turn=0;turn<record.turnEntryDistances.Length;turn++) foreach(float approach in new[] {10f,15f,20f})
                    {
                        generator.SetSpawn(episode,2,17,turn,10,20,.25f,turn,approach); progress.ResetProgress();
                        Check(Mathf.Abs(record.turnEntryDistances[turn]-record.spawnDistance-approach)<.001f,"near actual turn entry");
                        Check(Mathf.Abs(progress.Project(track.spawn.position,out _)-record.spawnDistance)<.001f,"spawn on centerline");
                        Check(progress.TravelMetres==0 && progress.ValidatedGates==0 && progress.RewardPosition==0,"midroute start has zero skipped credit");
                        Check(record.activeGateIndices[0]==track.firstCheckpoint && record.activeGateIndices[record.activeGateIndices.Length-1]==0,"ordered active suffix");
                        Check(Mathf.Abs(record.taskDistance-(record.finishDistance-record.spawnDistance))<.001f,"remaining task length");
                        if(track.firstCheckpoint!=1) Check(!progress.ValidateGate(1),"skipped early gates cannot validate");
                        Vector3 target=ProceduralTrack.At(record,record.gateDistances[track.firstCheckpoint],out _);
                        Check(progress.Advance(target,record.length) && progress.ValidateGate(track.firstCheckpoint),"first remaining gate validates");
                    }
                    if(seed==101 || seed==211 || seed==307)
                    {
                        var spawnStats=new SpawnCoverage {seed=seed,turnCounts=new int[record.turnEntryDistances.Length]};
                        for(int drawIndex=0;drawIndex<spawnStats.draws;drawIndex++)
                        {
                            generator.SetSpawn(episode,1,7,drawIndex,10,20,.25f,-1,15);
                            if(record.spawnTurnIndex<0) {spawnStats.originalCount++;Check(record.spawnApproach==0 && record.spawnDistance==4,"sampled original spawn");continue;}
                            spawnStats.nearTurnCount++; spawnStats.turnCounts[record.spawnTurnIndex]++;
                            if(record.turnAngles[record.spawnTurnIndex]<0) spawnStats.leftCount++; else spawnStats.rightCount++;
                            Check(record.spawnApproach>=10 && record.spawnApproach<=20,"sampled approach in10–20m");
                            Check(Mathf.Abs(record.turnEntryDistances[record.spawnTurnIndex]-record.spawnDistance-record.spawnApproach)<.001f,"sampled pose before selected turn entry");
                        }
                        spawnStats.nearTurnFraction=(float)spawnStats.nearTurnCount/spawnStats.draws;
                        Check(spawnStats.originalCount>0 && spawnStats.leftCount>0 && spawnStats.rightCount>0,"sampled original/left/right coverage");
                        foreach(int count in spawnStats.turnCounts) Check(count>0,"all sampled turn indices reachable");
                        Check(spawnStats.nearTurnFraction>=.65f && spawnStats.nearTurnFraction<=.85f,"400-draw sampled fraction near75percent");
                        coverage.Add(spawnStats);
                    }
                    generator.SetSpawn(episode,1,17,12,10,20,.25f,-1,15); string taskIdentity=record.taskSha256;
                    generator.SetSpawn(episode,1,17,12,10,20,.25f,-1,15); Check(record.taskSha256==taskIdentity,"spawn determinism independent of generator retries");

                }
                generator.Configure(track,episode,0,0,parameters);
                Check(track.closedLoop && fixedSpawn.gameObject.activeSelf && track.spawn==fixedSpawn,"fixed mode restored");
                Check(root.GetComponentsInChildren<MeshCollider>().Length==0,"no active generated colliders in fixed mode");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--racing-track-output");
            string output=index>=0 ? args[index+1] : "../runs/procedural/geometry.json";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,JsonUtility.ToJson(new Summary {generatedSeeds=1000,geometrySha256=hashes.ToArray(),representativeTracks=representative.ToArray(),spawnCoverage=coverage.ToArray()},true));
            Debug.Log("PROCEDURAL_TRACK_VERIFICATION_SUCCESS: seeds=1000 output="+Path.GetFullPath(output));
        }
    }
}
