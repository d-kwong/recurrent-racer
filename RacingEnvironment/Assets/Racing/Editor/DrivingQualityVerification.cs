using System;
using UnityEngine;
namespace Racing.Editor
{
    public static class DrivingQualityVerification
    {
        static void Check(bool valid,string message) {if(!valid) throw new InvalidOperationException(message);}
        static DrivingQualityRecord Probe(int reverseTicks,int stallTicks,int offTicks)
        {
            var quality=new DrivingQuality(10,20);
            for(int tick=1;tick<=220;tick++)
                quality.Sample(.02f,tick*.02f,tick<=offTicks ? 5 : 0,4.5f,tick<=reverseTicks ? -1 : 1,
                    tick>100 && tick<=100+stallTicks ? 0 : 1,tick);
            return quality.Complete(true,220);
        }
        public static void Run()
        {
            Check(Probe(25,100,0).cleanFinish,"exact reverse/stall limits allowed");
            Check(!Probe(26,100,0).cleanFinish,"26 reverse ticks rejected");
            Check(!Probe(25,101,0).cleanFinish,"101 stall ticks rejected");
            var launch=new DrivingQuality(10,20);
            for(int tick=1;tick<=100;tick++) launch.Sample(.02f,tick*.02f,0,4.5f,0,0,0);
            Check(launch.Complete(true,0).maximumStallSeconds==0,"2s launch grace");
            foreach(int off in new[] {5,6})
            {
                var quality=new DrivingQuality(10,20);
                for(int tick=1;tick<=100;tick++) quality.Sample(.02f,tick*.02f,tick<=off ? 5 : 0,4.5f,1,1,tick);
                var result=quality.Complete(true,100);
                Check(result.cleanFinish==(off==5),"5% asphalt boundary"); Check(result.ticks==100 && result.offAsphaltTicks==off,"actual tick denominator");
                Check(result.firstCornerPassed && result.failureDistanceToFirstCorner==-90,"first corner and signed failure distance");
            }
            var fresh=new DrivingQuality(10,20);fresh.Sample(.02f,.02f,0,4.5f,1,1,19);
            var record=fresh.Complete(false,19);
            Check(record.ticks==1 && !record.firstCornerPassed && !record.cleanFinish,"fresh reset and nonfinish");
            Debug.Log("DRIVING_QUALITY_VERIFICATION_SUCCESS: passive helper thresholds/ticks/reset/corner/endings");
        }
    }
}
