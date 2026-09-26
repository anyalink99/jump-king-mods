using System;
using BehaviorTree;

namespace JKRuntime.UI
{
    /// <summary>One native menu-tree lifetime shared by native and presentation ticks.
    /// Completion is retained until the caller creates a new session for a new run.</summary>
    public sealed class MenuTreeSession
    {
        private readonly BTmanager tree;
        private bool started, pending, completed, running, faulted;
        private BTresult result = BTresult.NULL;
        public MenuTreeSession(BTmanager value)
        { if(value==null) throw new ArgumentNullException("value"); tree=value; }
        public BTmanager Tree { get { return tree; } }
        public bool CanPump { get { return started && !completed && !faulted && !running; } }

        /// <summary>Native parent consumes a presentation result before advancing again.
        /// Disabling fast mode must preserve this handoff, including a terminal result.</summary>
        public BTresult RunNative(float delta, bool presentationOwnsTree)
        {
            Check();
            if(pending) { pending=false; return result; }
            if(completed || started && presentationOwnsTree) return result;
            return Advance(delta);
        }
        /// <summary>Advance an already-started running tree. Never restart a finished tree.</summary>
        public void Pump(float delta)
        {
            Check();
            if(!CanPump) return;
            Advance(delta); pending=true;
        }
        private void Check()
        {
            RuntimeApi.Kernel.CheckThread();
            if(running) throw new InvalidOperationException("Reentrant menu tree dispatch");
            if(faulted) throw new InvalidOperationException("Faulted menu tree requires a new native lifetime");
        }
        private BTresult Advance(float delta)
        {
            running=true;
            try
            {
                result=tree.Run(delta); started=true;
                completed=result!=BTresult.Running;
                return result;
            }
            catch { faulted=true; throw; }
            finally { running=false; }
        }
    }
}
