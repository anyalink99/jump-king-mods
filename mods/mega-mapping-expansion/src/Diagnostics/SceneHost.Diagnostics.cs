using System;
using System.Diagnostics;
using System.Globalization;

namespace MegaMappingExpansion
{
    internal sealed partial class SceneHost
    {
        private long canopyProfileTicks, lightProfileTicks;
        private long queueProfileTicks, waterProfileTicks, compositorProfileTicks;
        private long profileStart = Stopwatch.GetTimestamp();
        private long previousFrame;
        private readonly double[] frameIntervals = new double[600];
        private int intervalCount, intervalCursor;
        private int gen0Start = GC.CollectionCount(0), gen1Start = GC.CollectionCount(1), gen2Start = GC.CollectionCount(2);
        private int profileFrames;
        internal long NativeDrawTicks;
        internal long NativePresentTicks;
        private long nativeUpdateTicks,maxDrawTicks,maxUpdateTicks,maxPresentTicks;
        private int profileScreen=-1;
        private bool profileActive;
        private Microsoft.Xna.Framework.Vector2 profilePosition;
        private bool hasProfilePosition;
        private float profileTravel;
        internal void ProfileStage(int stage,long ticks)
        {
            if(!scene.Options.Profiling)return;
            if(stage==0){NativeDrawTicks+=ticks;maxDrawTicks=Math.Max(maxDrawTicks,ticks);}
            else if(stage==1){nativeUpdateTicks+=ticks;maxUpdateTicks=Math.Max(maxUpdateTicks,ticks);}
            else{NativePresentTicks+=ticks;maxPresentTicks=Math.Max(maxPresentTicks,ticks);}
        }
        private void ResetProfile(long now)
        {
            profileStart=now;previousFrame=now;profileFrames=0;
            NativeDrawTicks=NativePresentTicks=nativeUpdateTicks=maxDrawTicks=maxUpdateTicks=maxPresentTicks=0;
            canopyProfileTicks=lightProfileTicks=queueProfileTicks=waterProfileTicks=compositorProfileTicks=0;
            gen0Start=GC.CollectionCount(0);gen1Start=GC.CollectionCount(1);gen2Start=GC.CollectionCount(2);
            intervalCount=intervalCursor=0;
            profileTravel=0;hasProfilePosition=false;
            foreach(var plan in renderPlans.Values)
                foreach(var layer in new[]{plan.Background,plan.World,plan.Foreground})
                    foreach(var command in layer)command.ProfileTicks=0;
        }
        internal void RecordFrameCost()
        {
            if (!scene.Options.Profiling) return;
            long now = Stopwatch.GetTimestamp();
            // Never attribute a previous room or an inactive interval to active
            // gameplay. EndDraw owns this sample, after all measured stages finish.
            int current=JumpKing.Camera.CurrentScreenIndex1;bool active=JumpKing.Game1.instance.IsActive;
            if(profileScreen!=current||profileActive!=active)
            {profileScreen=current;profileActive=active;ResetProfile(now);return;}
            profileFrames++;
            var player=GameLoopPlayer();
            if(player!=null)
            {
                var position=player.m_body.Position;
                if(hasProfilePosition)profileTravel+=Microsoft.Xna.Framework.Vector2.Distance(position,profilePosition);
                profilePosition=position;hasProfilePosition=true;
            }
            if (previousFrame != 0)
            {
                frameIntervals[intervalCursor] = (now - previousFrame) * 1000.0 / Stopwatch.Frequency;
                intervalCursor = (intervalCursor + 1) % frameIntervals.Length;
                intervalCount = Math.Min(intervalCount + 1, frameIntervals.Length);
            }
            previousFrame = now;
            double elapsed = (now - profileStart) / (double)Stopwatch.Frequency;
            if (elapsed < 5) return;
            double factor = 1000.0 / Stopwatch.Frequency / profileFrames;
            double[] sorted = new double[intervalCount]; Array.Copy(frameIntervals, sorted, intervalCount); Array.Sort(sorted);
            double p95 = sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * .95)];
            double p99=sorted.Length==0?0:sorted[(int)((sorted.Length-1)*.99)];
            double maximum=sorted.Length==0?0:sorted[sorted.Length-1];
            int missed=0;foreach(double interval in sorted)if(interval>1000.0/58)missed++;
            long textureBytes = 0;
            foreach (SceneTexture texture in textures.Values) foreach(var page in texture.Pages) textureBytes += (long)page.Width * page.Height * 4;
            foreach (SceneTexture texture in vectors.Values) textureBytes += (long)texture.Texture.Width * texture.Texture.Height * 4;
            ModEntry.Log(string.Format(CultureInfo.InvariantCulture,
                "Graphics profile: drawFPS={0:F1}, frameP95={1:F2}ms, queues={2:F2}ms, canopy={3:F2}ms, lights={4:F2}ms, water={5:F2}ms, compositor={6:F2}ms, GC={7}/{8}/{9}, managed={10:F1}MiB, baseTextures={11:F1}MiB, meshes={12}, rimFrames={13}, targets={14}, gameActive={15}, targetStep={16:F2}ms, screen={17}",
                profileFrames / elapsed, p95, queueProfileTicks * factor, canopyProfileTicks * factor,
                lightProfileTicks * factor, waterProfileTicks * factor, compositorProfileTicks * factor,
                GC.CollectionCount(0)-gen0Start, GC.CollectionCount(1)-gen1Start, GC.CollectionCount(2)-gen2Start,
                GC.GetTotalMemory(false) / 1048576.0, textureBytes / 1048576.0, canopyMeshes.Count, rimFrames.Count,
                (compositeTarget == null ? 0 : 1) + (lightTarget == null ? 0 : 1),JumpKing.Game1.instance.IsActive,
                JumpKing.Game1.instance.TargetElapsedTime.TotalMilliseconds,JumpKing.Camera.CurrentScreenIndex1));
            ModEntry.Log("Native Draw CPU: "+(NativeDrawTicks*factor).ToString("F2",CultureInfo.InvariantCulture)
                +"ms; Present: "+(NativePresentTicks*factor).ToString("F2",CultureInfo.InvariantCulture)
                +"ms; interval="+JumpKing.Game1.instance.GraphicsDevice.PresentationParameters.PresentationInterval);
            double ms=1000.0/Stopwatch.Frequency;
            ModEntry.Log(string.Format(CultureInfo.InvariantCulture,
                "Frame pacing: active={0}, screen={1}, p99={2:F2}ms, max={3:F2}ms, over58Budget={4}/{5}, Update={6:F2}ms, peakUpdate={7:F2}ms, peakDraw={8:F2}ms, peakPresent={9:F2}ms",
                active,current,p99,maximum,missed,sorted.Length,nativeUpdateTicks*factor,maxUpdateTicks*ms,maxDrawTicks*ms,maxPresentTicks*ms));
            ScreenRenderPlan plan;
            if(renderPlans.TryGetValue(current,out plan))
            {
                var commands=new System.Collections.Generic.List<RenderCommand>();
                commands.AddRange(plan.Background);commands.AddRange(plan.World);commands.AddRange(plan.Foreground);
                commands.Sort(delegate(RenderCommand a,RenderCommand b){return b.ProfileTicks.CompareTo(a.ProfileTicks);});
                string detail="";
                for(int i=0;i<Math.Min(4,commands.Count);i++)detail+="; "+commands[i].Id+"="+(commands[i].ProfileTicks*factor).ToString("F2",CultureInfo.InvariantCulture)+"ms";
                ModEntry.Log("Active movement sample: travel="+profileTravel.ToString("F1",CultureInfo.InvariantCulture)+"px"+detail);
            }
            ResetProfile(now);
        }
    }
}
