using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MegaMappingExpansion
{
    // no game state here; previews and real crossings use the same selection rules
    internal sealed class TransitionChoice
    {
        internal ulong Random;
        internal int Cursor, Current = -1;
        internal readonly List<int> History = new List<int>(), Bag = new List<int>();
        internal TransitionChoice Copy()
        {
            var value = new TransitionChoice { Random = Random, Cursor = Cursor, Current = Current };
            value.History.AddRange(History); value.Bag.AddRange(Bag); return value;
        }
    }

    internal static class TransitionSelection
    {
        internal static ulong Seed(ulong seed, string id)
        {
            ulong value = 14695981039346656037UL ^ seed;
            foreach (byte part in Encoding.UTF8.GetBytes(id)) { value ^= part; value *= 1099511628211UL; }
            return value == 0 ? 1 : value;
        }
        private static uint Next(TransitionChoice state, uint bound)
        {
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                ulong value = state.Random;
                value ^= value >> 12; value ^= value << 25; value ^= value >> 27; state.Random = value;
                uint sample = (uint)((value * 2685821657736338717UL) >> 32);
                if (sample >= threshold) return sample % bound;
            }
        }
        internal static int Pick(TransitionPool pool, TransitionChoice state)
        {
            int count = pool.Count;
            var candidates = Enumerable.Range(0, count).Where(i => !state.History.Contains(i)).ToArray();
            if (candidates.Length == 0) throw new InvalidDataException("No eligible choice in pool " + pool.Id);
            int selected;
            if (pool.Mode == "sequence") { selected = state.Cursor; state.Cursor = (state.Cursor + 1) % count; }
            else if (pool.Mode == "shuffle")
            {
                if (state.Bag.Count == 0) state.Bag.AddRange(Enumerable.Range(0, count));
                candidates = state.Bag.Where(i => !state.History.Contains(i)).ToArray();
                if (candidates.Length == 0) throw new InvalidDataException("Shuffle has no eligible choice in pool " + pool.Id);
                selected = candidates[Next(state, (uint)candidates.Length)]; state.Bag.Remove(selected);
            }
            else
            {
                uint total = 0;
                foreach (int i in candidates) total += (uint)pool.Weights[i];
                uint sample = Next(state, total); selected = candidates[0];
                foreach (int i in candidates) { if (sample < pool.Weights[i]) { selected = i; break; } sample -= (uint)pool.Weights[i]; }
            }
            state.Current = selected;
            state.History.Add(selected);
            while (state.History.Count > pool.AvoidLast) state.History.RemoveAt(0);
            return selected;
        }
    }
}
