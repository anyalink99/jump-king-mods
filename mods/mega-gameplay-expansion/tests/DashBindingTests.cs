using System;
using System.IO;
using System.Xml.Serialization;
using JumpKing.Controller;
using JumpKing.Player;
using EntityComponent;

namespace MegaGameplayExpansion
{
    internal static partial class Tests
    {
        private sealed class BindingPad : IPad
        {
            internal string Id="keyboard";
            internal int[] Buttons=new int[0];
            internal bool Connected=true;
            public int[] GetPressedButtons() { return Buttons; }
            public string ButtonToString(int value) { return value.ToString(); }
            public PadBinding GetDefaultBind() { return new PadBinding { jump=new[]{32} }; }
            public string GetSaveIdentifier() { return Id; }
            public string GetPrintName() { return Id; }
            public bool IsConnected() { return Connected; }
        }
        private static void DashBindingRegression()
        {
            var oldBindings=Settings.Current.DashBindings;
            bool oldDash=Settings.Current.AirDash;
            try
            {
                foreach(int held in new[]{-1,0,1}) foreach(int velocity in new[]{-1,0,1}) foreach(int facing in new[]{-1,1})
                    Require(AirDashController.ChooseDirection(held,velocity*3.5f,facing)==(held!=0?held:velocity!=0?velocity:facing),
                        "Dash direction must prioritize current directional input, then flight, then facing");
                Settings.Current.DashBindings=new[]{new DashDeviceBinding { Device="keyboard",Chords=new[]{new[]{77}} }};
                var pad=new PadInstance(new BindingPad());
                var controllerPad=new PadInstance(new BindingPad { Id="controller" });
                Require(DashBindings.Get(pad)[0][0]==77 && DashBindings.Get(controllerPad)[0][0]==32,"Dash binding crossed device profiles");
                controllerPad.GetBind().jump=new[]{10};
                Require(DashBindings.Get(controllerPad)[0][0]==10,"Default dash stopped following changed Jump binding");
                var serializer=new XmlSerializer(typeof(Preferences));
                using(var stream=new MemoryStream()) {
                    serializer.Serialize(stream,Settings.Current); stream.Position=0;
                    var copy=(Preferences)serializer.Deserialize(stream);
                    Require(copy.DashBindings[0].Device=="keyboard" && copy.DashBindings[0].Chords[0][0]==77,"Dash bindings lost in XML round trip");
                }
                int[][] separate={new[]{77}}, jump={new[]{32}}, shared={new[]{32}}, chord={new[]{32,77}};
                var input=new DashInput();
                Require(!input.Observe(new[]{77},separate,jump).Pressed,"Initial held dash generated a press");
                input.Observe(new int[0],separate,jump);
                var press=input.Observe(new[]{32,77},separate,jump);
                Require(press.Pressed && !press.SharedJump,"Separate dash stole simultaneous Jump");
                Require(!input.Observe(new[]{32,77},separate,jump).Pressed,"Held dash repeated");
                input.Reset(); Require(!input.Observe(new[]{77},separate,jump).Pressed,"Resume/restore held dash generated a press");
                input.Observe(new int[0],shared,jump);
                press=input.Observe(new[]{32},shared,jump);
                Require(press.Pressed && press.SharedJump,"Overlapping dash failed to claim Jump");
                input.Observe(new int[0],chord,jump);
                Require(!input.Observe(new[]{32},chord,jump).Pressed,"Incomplete dash chord activated");
                press=input.Observe(new[]{32,77},chord,jump);
                Require(press.Pressed && press.SharedJump,"Shared chord failed to claim Jump");
                int mouse=JKRuntime.Input.MouseButtons.Left;
                input.Observe(new int[0],new[]{new[]{mouse}},jump);
                press=input.Observe(new[]{mouse,32},new[]{new[]{mouse}},jump);
                Require(press.Pressed && !press.SharedJump,"Mouse dash stole keyboard buffer");

                var customDevice=new BindingPad();
                var customPad=new PadInstance(customDevice);
                var multi=new DashInput();
                var allPads=new[]{customPad,controllerPad};
                multi.ReadDevices(allPads,false);
                customDevice.Buttons=new[]{77};
                typeof(PadInstance).GetField("current_state",Flags).SetValue(controllerPad,new PadState { jump=true });
                press=multi.ReadDevices(allPads,true);
                Require(press.Pressed && !press.SharedJump,"Separate cross-device dash lost simultaneous Jump buffer");
                customDevice.Connected=false; multi.ReadDevices(allPads,false);
                customDevice.Connected=true;
                Require(!multi.ReadDevices(allPads,false).Pressed,"Reconnect with held button generated a dash");
                customDevice.Buttons=new int[0]; multi.ReadDevices(allPads,false);
                customDevice.Buttons=new[]{77};
                Require(multi.ReadDevices(allPads,false).Pressed,"Reconnected custom device never rearmed");
                multi.ReadDevices(new[]{controllerPad},false);
                Require(!multi.ReadDevices(allPads,false).Pressed,"Re-added held device generated a dash");

                // NativePause sends observations every tick, not just transitions.
                // Exercise the controller's own reader and subscription together.
                using(var fixture=new WalkInputFixture()) foreach(bool pumpBeforeInput in new[]{false,true})
                {
                    Settings.Current.AirDash=true;
                    DashWorld(); var player=DashPlayer();
                    var device=new BindingPad(); var boundPad=new PadInstance(device);
                    DashInput ownedReader=null;
                    var observePause=typeof(JKRuntime.Gameplay.NativePause).GetMethod("Observe",Flags);
                    using(var dash=new AirDashController(player,null,null,
                        delegate(bool native) { return ownedReader.ReadDevices(new[]{boundPad},native); }))
                    {
                        ownedReader=NativeFlight.Get<DashInput>(dash,"dashInput");
                        observePause.Invoke(null,null);
                        dash.AfterInput(1f/60f);
                        var pauseTransition=typeof(AirDashController).GetMethod("ObservePause",Flags);
                        pauseTransition.Invoke(dash,new object[]{true,0L});
                        device.Buttons=new[]{77};
                        pauseTransition.Invoke(dash,new object[]{false,1L});
                        dash.AfterInput(1f/60f);
                        Require(!dash.Active,"Held custom binding triggered a dash on resume");
                        for(int tick=0;tick<4;tick++) {
                            device.Buttons=tick==3?new[]{77}:new int[0];
                            if(pumpBeforeInput) observePause.Invoke(null,null);
                            dash.AfterInput(1f/60f);
                            if(!pumpBeforeInput) observePause.Invoke(null,null);
                            Require(dash.Active==(tick==3),"Per-tick pause observations suppressed the separate Dash binding");
                        }
                    }
                }

                using(var fixture=new WalkInputFixture())
                {
                    Settings.Current.AirDash=true;
                    foreach(int held in new[]{-1,1}) {
                        DashWorld(); var directed=DashPlayer(); directed.m_body.Velocity.X=-held*3.5f;
                        fixture.Direction(held);
                        using(var dash=new AirDashController(directed,null,null,delegate(bool native) { return new DashPress(true,false); })) {
                            dash.AfterInput(1f/60f);
                            dash.ExecuteBehaviour(NativeFlight.Get<JumpKing.BodyCompBehaviours.BehaviourContext>(directed.m_body,"m_behaviourContext"));
                            Require(directed.m_body.Position.X==180+held*AirDashController.Speed,"Held opposite input did not reverse actual dash movement");
                        }
                    }
                }
                using(var fixture=new WalkInputFixture()) foreach(bool overlap in new[]{false,true})
                {
                    Settings.Current.AirDash=true;
                    DashWorld();
                    var player=DashPlayer();
                    var reader=new DashInput(); int[] physical=new int[0];
                    int[][] binding=overlap?shared:separate;
                    reader.Observe(physical,binding,jump);
                    using(var dash=new AirDashController(player,null,null,delegate(bool native) { return reader.Observe(physical,binding,jump); }))
                    {
                        physical=overlap?new[]{32}:new[]{32,77};
                        DashPress(dash,player,fixture);
                        Require(dash.Active,"Custom binding did not start dash");
                        Require(NativeFlight.Get<bool>(player.GetComponent<InputComponent>(),"_can_jump")==!overlap,
                            "Dash consumed an unrelated native buffer or retained shared activation");
                        if(!overlap) {
                            // Native charge is allowed at the next landing without
                            // releasing/repressing the independently held Jump.
                            NativeFlight.Set(player.m_body,"_is_on_ground",true);
                            var node=new JumpState(player);
                            Require(node.Run(new BehaviorTree.TickData { delta_time=1f/60f })==BehaviorTree.BTresult.Running,
                                "Separate dash lost immediate buffered charge");
                        }
                    }
                }
                Console.WriteLine("[OK] Dash bindings: device isolation, XML, dynamic Jump default, per-tick pause observations, resume edges, chords/mouse, shared press and independent native buffer");
            }
            finally { Settings.Current.DashBindings=oldBindings; Settings.Current.AirDash=oldDash; }
        }
    }
}
