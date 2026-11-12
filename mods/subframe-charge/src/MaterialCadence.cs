using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Player;

namespace SubframeCharge
{
    // registrations tell us which contacts a callback owns, not its class name
    internal sealed class MaterialCadence
    {
        private static readonly FieldInfo Lookup=AccessTools.Field(typeof(BodyComp),"m_blockBehaviourLookup");
        private readonly Dictionary<IBlockBehaviour,Entry> entries=new Dictionary<IBlockBehaviour,Entry>();
        private BodyComp body;
        private Dictionary<Type,IBlockBehaviour> lookup;
        private AdvCollisionInfo collision;
        private IReadOnlyList<IBlock> contacts;
        private sealed class Entry
        {
            internal readonly List<IBlock> Contacts=new List<IBlock>(), Next=new List<IBlock>();
            internal float Elapsed;
            internal bool Started, Result=true;
        }
        internal void Reset() { entries.Clear(); body=null; lookup=null; collision=null; contacts=null; }
        internal bool Due(IBlockBehaviour callback,BehaviourContext context,float delta,bool native,out float elapsed)
        {
            if(!ReferenceEquals(body,context.BodyComp)) {
                Reset(); body=context.BodyComp; lookup=(Dictionary<Type,IBlockBehaviour>)Lookup.GetValue(body);
            }
            Entry entry;
            if(!entries.TryGetValue(callback,out entry)) {
                entry=new Entry {Elapsed=1f/60-delta}; entries.Add(callback,entry);
            }
            entry.Elapsed+=delta;
            entry.Next.Clear();
            var info=context.CollisionInfo.PreResolutionCollisionInfo;
            // native GetCollidedBlocks allocates its view, share one per physical pass
            if(!ReferenceEquals(collision,info)) {collision=info;contacts=info==null?null:info.GetCollidedBlocks();}
            for(int i=0;contacts!=null && i<contacts.Count;i++) {
                var block=contacts[i];
                foreach(var pair in lookup) if(ReferenceEquals(pair.Value,callback) && pair.Key==block.GetType()) {
                    entry.Next.Add(block); break;
                }
            }
            bool changed=entry.Next.Count!=entry.Contacts.Count;
            if(!changed) for(int i=0;i<entry.Next.Count;i++) if(!ReferenceEquals(entry.Next[i],entry.Contacts[i])) {changed=true;break;}
            elapsed=entry.Elapsed;
            if(entry.Started && !native && !changed) return false;
            entry.Contacts.Clear(); entry.Contacts.AddRange(entry.Next);
            entry.Elapsed=0; entry.Started=true;
            return true;
        }
        internal bool Result(IBlockBehaviour callback) { return entries[callback].Result; }
        internal void Complete(IBlockBehaviour callback,bool result) { entries[callback].Result=result; }
    }
}
