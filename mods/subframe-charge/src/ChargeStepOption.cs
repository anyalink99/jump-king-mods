using BehaviorTree;
using JumpKing.PauseMenu.BT.Actions;

namespace SubframeCharge
{
    public sealed class ChargeStepOption : IOptions
    {
        private bool syncing;
        private static readonly string[] labels = { "Off", "Half", "Quarter", "Eighth", "1 ms" };
        public ChargeStepOption() : base(5, Options.Substeps.Value, EdgeMode.Wrap) { }
        protected override bool CanChange() { return Options.Enabled.Value; }
        protected override string CurrentOptionName() { return "Substep Charge: " + labels[Options.Substeps.Value]; }
        protected override void OnOptionChange(int option)
        {
            if (!syncing && CanChange()) Options.Substeps.Set(option);
        }
        private void Sync()
        {
            // pause and title menus can keep separate instances of this row
            syncing = true;
            try { CurrentOption = Options.Substeps.Value; }
            finally { syncing = false; }
        }
        protected override BTresult MyRun(TickData data) { Sync(); return base.MyRun(data); }
        public override void Draw(int x, int y, bool selected) { Sync(); base.Draw(x, y, selected); }
    }
}
