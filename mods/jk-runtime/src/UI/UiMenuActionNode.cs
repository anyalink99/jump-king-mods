using System;
using BehaviorTree;
using JumpKing.PauseMenu.BT;

namespace JKRuntime.UI
{
    internal sealed class UiMenuActionNode : IBTnode
    {
        private readonly Func<bool> execute;

        internal UiMenuActionNode(Func<bool> action)
        {
            execute = action;
        }

        protected override BTresult MyRun(TickData data)
        {
            return execute() ? BTresult.Success : BTresult.Failure;
        }
    }

    internal sealed class UiMenuFeedbackActionNode : IBTnode
    {
        private readonly Func<UiMenuActionResult> execute;
        private readonly Func<long> utcTicks;
        private string feedbackLabel = string.Empty;
        private long feedbackUntil;
        private Func<UiMenuActionResult> pending;

        internal UiMenuFeedbackActionNode(
            Func<UiMenuActionResult> action)
            : this(action, delegate { return DateTime.UtcNow.Ticks; })
        {
        }

        internal UiMenuFeedbackActionNode(
            Func<UiMenuActionResult> action,
            Func<long> clock)
        {
            if (action == null) throw new ArgumentNullException("action");
            if (clock == null) throw new ArgumentNullException("clock");
            execute = action;
            utcTicks = clock;
        }

        internal string Label(string ordinaryLabel)
        {
            if (pending != null)
            {
                UiMenuActionResult result;
                try { result = pending(); }
                catch (Exception error) { Console.WriteLine("[JK Runtime UI] Pending action failed: " + error); result = UiMenuActionResult.Rejected("Failed", 3f); }
                if (result == null) return feedbackLabel;
                Apply(result);
            }
            return feedbackUntil > utcTicks()
                ? feedbackLabel
                : ordinaryLabel;
        }

        protected override BTresult MyRun(TickData data)
        {
            if (pending != null) return BTresult.Failure;
            UiMenuActionResult result = execute();
            if (result == null) return BTresult.Failure;
            Apply(result);
            return result.Succeeded ? BTresult.Success : BTresult.Failure;
        }

        internal void Apply(UiMenuActionResult result)
        {
            if (result == null) return;
            pending = result.Poll;
            if (pending != null) { feedbackLabel = result.FeedbackLabel; return; }
            if (!string.IsNullOrWhiteSpace(result.FeedbackLabel)
                && result.FeedbackDurationSeconds > 0f)
            {
                feedbackLabel = result.FeedbackLabel;
                feedbackUntil = utcTicks()
                    + (long)(result.FeedbackDurationSeconds * TimeSpan.TicksPerSecond);
            }
        }
    }

    internal sealed class UiFeedbackTextButton : TextButton
    {
        private readonly string ordinaryLabel;
        private readonly UiMenuFeedbackActionNode action;

        internal UiFeedbackTextButton(
            string label,
            UiMenuFeedbackActionNode node)
            : base(label, node)
        {
            ordinaryLabel = label;
            action = node;
        }

        public override void Draw(int x, int y, bool selected)
        {
            Text = action.Label(ordinaryLabel);
            base.Draw(x, y, selected);
        }

        public override Microsoft.Xna.Framework.Point GetSize()
        {
            Text = action.Label(ordinaryLabel);
            return base.GetSize();
        }
    }
}
