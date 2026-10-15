using BehaviorTree;
using JumpKing.PauseMenu.BT.Actions;

namespace SubframeCharge
{
    public sealed class ChargeStepOption : IOptions
    {
        private bool syncing;
        public ChargeStepOption() : base(4, (int)Options.ChargeStep.Value, EdgeMode.Wrap) { }
        protected override bool CanChange() { return Options.Enabled.Value && Options.QuarterSteps.Value; }
        protected override string CurrentOptionName() { return "Charge Step: " + Substep.Label(Options.ChargeStep.Value); }
        protected override void OnOptionChange(int option)
        {
            if (!syncing && CanChange()) Options.ChargeStep.Set((SubstepMode)option);
        }
        private void Sync()
        {
            // pause and title menus can keep separate instances of this row
            syncing = true;
            try { CurrentOption = (int)Options.ChargeStep.Value; }
            finally { syncing = false; }
        }
        protected override BTresult MyRun(TickData data) { Sync(); return base.MyRun(data); }
        public override void Draw(int x, int y, bool selected) { Sync(); base.Draw(x, y, selected); }
    }
}
