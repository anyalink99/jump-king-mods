using BehaviorTree;
using JumpKing.PauseMenu.BT.Actions;

namespace SubframeCharge
{
    public sealed class SubframeModeOption : IOptions
    {
        private bool syncing;
        public SubframeModeOption() : base(3,(int)Options.Mode.Value,EdgeMode.Wrap) { }
        protected override bool CanChange() { return !PerformanceSuspension.Active; }
        protected override string CurrentOptionName()
        {
            return "Mode: " + SubframeModes.Label(Options.Mode.Value);
        }
        protected override void OnOptionChange(int option) { if(!syncing && CanChange()) Options.Mode.Set((SubframeMode)option); }
        private void Sync() { syncing=true; try { CurrentOption=(int)Options.Mode.Value; } finally { syncing=false; } }
        protected override BTresult MyRun(TickData data) { Sync(); return base.MyRun(data); }
        public override void Draw(int x,int y,bool selected) { Sync(); base.Draw(x,y,selected); }
    }
}
