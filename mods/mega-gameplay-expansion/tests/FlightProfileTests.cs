using System;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private static void FlightProfileRegression()
        {
            foreach (bool water in new[] { false, true })
            foreach (int direction in new[] { -1, 0, 1 })
            {
                var blocks = new System.Collections.Generic.List<IBlock> { new BoxBlock(new Rectangle(0,320,480,40)) };
                if (water) blocks.Add(new WaterBlock(new Rectangle(0,-300,480,620)));
                var screens = Scene(blocks.ToArray());
                var source = new BodyComp(new Vector2(180,294),18,26) { Velocity = new Vector2(direction*3.5f,-6) };
                FlightProfile.StopSession();
                var expected = new FlightJob(new FlightJob.Seed(source,new FlightWorld(screens,0,0)));
                while (!expected.Done) expected.Step();
                using (FlightProfile.StartSession())
                {
                    var actual = new FlightJob(new FlightJob.Seed(source,new FlightWorld(screens,0,0)));
                    while (!actual.Done) actual.Step();
                    Require(actual.Failure==null && expected.Failure==null,"Profile forecast failed");
                    Require(actual.Ticks==expected.Ticks && actual.Landing.Position==expected.Landing.Position
                        && actual.Landing.Velocity==expected.Landing.Velocity && actual.Landing.LastVelocity==expected.Landing.LastVelocity,
                        "Diagnostic markers changed native trajectory");
                    Require(source.Position==new Vector2(180,294),"Profile mutated source");
                }
            }
            using (FlightProfile.StartSession())
            {
                var profile=FlightProfile.Create();
                profile.Enter(); profile.Begin(profile.Add("caught-exception-test"));
                var foreign = new System.Threading.Thread(delegate() {
                    for (int i=0;i<100;i++) try { throw new Exception("Other worker"); } catch (Exception) { }
                });
                foreign.Start(); foreign.Join();
                try { throw new InvalidOperationException("diagnostic fixture"); } catch (InvalidOperationException) { }
                profile.Leave();
                Require(profile.Summary(0,0,0,null).Contains("firstChance caught-exception-test:System.InvalidOperationException count=1"),
                    "Caught exception was not attributed to its stage");
                Require(!profile.Summary(0,0,0,null).Contains("System.Exception"), "Other threads cannot write forecast diagnostics");
                for(int i=1;i<12;i++) Require(FlightProfile.Create()!=null,"Profile limit too low");
                Require(FlightProfile.Create()==null,"Profile limit not enforced");
            }
            Require(FlightProfile.Create()==null,"Profile session leaked after disposal");
            Console.WriteLine("[OK] Diagnostic profile: six exact dry/water trajectories, unchanged source, caught exceptions, 12-forecast bound and cleanup");
        }
    }
}
