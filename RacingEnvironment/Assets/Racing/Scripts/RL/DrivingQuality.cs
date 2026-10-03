using System;
using UnityEngine;
namespace Racing
{
    [Serializable]
    public sealed class DrivingQualityRecord
    {
        public string version="physics-quality-v1";
        public int ticks, offAsphaltTicks;
        public float offAsphaltFraction, maximumReverseSeconds, maximumStallSeconds;
        public bool firstCornerPassed, cleanFinish;
        public float firstCornerEntryDistance, firstCornerExitDistance, failureDistanceToFirstCorner;
    }
    // Passive completed-interval measurements; never used by the environment objective or actor.
    public sealed class DrivingQuality
    {
        readonly DrivingQualityRecord record=new DrivingQualityRecord();
        float reverseSpan,stallSpan;
        public DrivingQuality(float firstEntry,float firstExit)
        {
            record.firstCornerEntryDistance=firstEntry; record.firstCornerExitDistance=firstExit;
        }
        public void Sample(float dt,float elapsed,float lateral,float asphaltHalfWidth,float forwardSpeed,float speed,float travel)
        {
            record.ticks++; if(lateral>asphaltHalfWidth) record.offAsphaltTicks++;
            reverseSpan=forwardSpeed<-.5f ? reverseSpan+dt : 0;
            stallSpan=elapsed>2f && speed<.5f ? stallSpan+dt : 0;
            record.maximumReverseSeconds=Mathf.Max(record.maximumReverseSeconds,reverseSpan);
            record.maximumStallSeconds=Mathf.Max(record.maximumStallSeconds,stallSpan);
            if(record.firstCornerExitDistance>=0 && travel>=record.firstCornerExitDistance) record.firstCornerPassed=true;
        }
        public DrivingQualityRecord Complete(bool finished,float travel)
        {
            record.offAsphaltFraction=record.ticks==0 ? 0 : (float)record.offAsphaltTicks/record.ticks;
            record.failureDistanceToFirstCorner=record.firstCornerEntryDistance-travel;
            // Tolerance avoids float accumulation turning exactly25/100 ticks into >0.5/2s.
            record.cleanFinish=finished && record.maximumReverseSeconds<=.5001f && record.maximumStallSeconds<=2.0001f && record.offAsphaltFraction<=.05f;
            return record;
        }
    }
}
