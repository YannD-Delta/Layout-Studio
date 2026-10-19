using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Undo/redo of constant changes. Records automatically by listening to the store.</summary>
    public class History
    {
        private class Change
        {
            public string Key;
            public string Before;
            public string After;
            public System.Action UndoAction;   // set for steps that are not a constant (a preset choice)
            public System.Action RedoAction;
        }

        private readonly ConstantStore store;
        private readonly int max;
        private readonly Dictionary<string, string> known = new Dictionary<string, string>();
        private readonly List<List<Change>> undo = new List<List<Change>>();
        private readonly List<List<Change>> redo = new List<List<Change>>();
        private List<Change> batch;
        private int batchDepth;
        private bool applying;

        public History(ConstantStore store, int max = 200)
        {
            this.store = store;
            this.max = max;
            foreach (var def in store.Definitions) known[def.Key] = store.Get(def.Key);
            store.ValueChanged += OnValueChanged;
        }

        /// <summary>Raised after the undo/redo stacks changed (a step was recorded, undone, redone or cleared).</summary>
        public event System.Action Changed;

        public int SubscriberCount { get { var h = Changed; return h == null ? 0 : h.GetInvocationList().Length; } }

        /// <summary>Remembers the handlers now attached; the returned action detaches every handler added after this call.</summary>
        public System.Action MarkSubscribers()
        {
            var before = Changed == null ? new System.Delegate[0] : Changed.GetInvocationList();
            return () =>
            {
                var now = Changed;
                if (now == null) return;
                foreach (var h in now.GetInvocationList())
                    if (System.Array.IndexOf(before, h) < 0) Changed -= (System.Action)h;
            };
        }

        private void NotifyChanged()
        {
            var handler = Changed;
            if (handler != null) handler();
        }

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public void BeginBatch()
        {
            if (batchDepth++ == 0) batch = new List<Change>();
        }

        public void EndBatch()
        {
            if (batchDepth == 0 || --batchDepth > 0) return;
            var finished = batch;
            batch = null;
            if (finished.Count > 0) Push(finished);
        }

        /// <summary>Closes every open batch: a drag that never completed must not leave the history unable to record.</summary>
        public void EndAllBatches()
        {
            while (batchDepth > 0) EndBatch();
        }

        /// <summary>
        /// Records something that is not a constant (a preset choice) as part of the current batch, or as its own step.
        /// The actions run while the history is replaying, so what they change is not recorded again.
        /// </summary>
        public void RecordAction(System.Action undoAction, System.Action redoAction)
        {
            var change = new Change { UndoAction = undoAction, RedoAction = redoAction };
            if (batch != null) batch.Add(change);
            else Push(new List<Change> { change });
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var step = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Apply(step.AsEnumerable().Reverse(), c => c.Before, c => c.UndoAction);
            redo.Add(step);
            NotifyChanged();
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var step = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Apply(step, c => c.After, c => c.RedoAction);
            undo.Add(step);
            NotifyChanged();
        }

        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            batch = null;
            batchDepth = 0;
            NotifyChanged();
        }

        private void OnValueChanged(string key)
        {
            string current = store.Get(key);
            if (applying)
            {
                known[key] = current;
                return;
            }
            var change = new Change { Key = key, Before = known[key], After = current };
            known[key] = current;
            if (batch != null)
            {
                var existing = batch.FirstOrDefault(c => c.Key == key);
                if (existing != null) existing.After = current;
                else batch.Add(change);
            }
            else
            {
                Push(new List<Change> { change });
            }
        }

        private void Push(List<Change> step)
        {
            undo.Add(step);
            redo.Clear();
            while (undo.Count > max) undo.RemoveAt(0);
            NotifyChanged();
        }

        private void Apply(IEnumerable<Change> changes, System.Func<Change, string> pick, System.Func<Change, System.Action> action)
        {
            applying = true;
            try
            {
                foreach (var c in changes)
                {
                    var run = action(c);
                    if (run != null) { run(); continue; }
                    string error;
                    store.TrySet(c.Key, pick(c), out error);
                }
            }
            finally
            {
                applying = false;
            }
        }
    }
}
