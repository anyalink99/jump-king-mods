using System;
using System.Reflection;
using System.Runtime.Serialization;
using JKRuntime.Presentation;
using JumpKing;
using JumpKing.Player;
using Microsoft.Xna.Framework;

namespace HitboxResizer.Patches
{
    // The fixture exposes the reviewed foreign geometry contract, without loading Workshop code.
    public static class PrefixPlayerEntityDraw
    {
        public static bool IsCustomHitbox {get;set;}
        public static int Width {get;set;}
        public static int Height {get;set;}
        public static bool Prefix(){return !IsCustomHitbox;}
    }
}
namespace JKRuntime
{
    internal static class AppearanceTests
    {
        private sealed class TestSprite:Sprite {}
        private static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
        private static void ForeignDraw(){}
        private static void Main(string[] args)
        {
            Assembly.LoadFrom(args[0]);
            var player=(PlayerEntity)FormatterServices.GetUninitializedObject(typeof(PlayerEntity));
            player.m_body=new BodyComp(new Vector2(180.25f,200.75f),10,14);
            var original=new TestSprite();player.SetSprite(original);
            using(var world=new RuntimeScope())
            {
                PlayerVisuals.Prepare(world);
                Check(AppearanceGeometry.Resolve(player).Anchor(player.m_body.Position)==new Vector2(189.25f,226.75f),"Native draw is not inferred from collision size");
                using(var foreign=new OwnedPatches("Phoenixx19.HitboxResizer.Harmony"))
                {
                    var type=typeof(HitboxResizer.Patches.PrefixPlayerEntityDraw);
                    foreign.Add(typeof(PlayerEntity).GetMethod("Draw"),prefix:type.GetMethod("Prefix"));
                    HitboxResizer.Patches.PrefixPlayerEntityDraw.IsCustomHitbox=true;
                    HitboxResizer.Patches.PrefixPlayerEntityDraw.Width=10;HitboxResizer.Patches.PrefixPlayerEntityDraw.Height=14;
                    var geometry=AppearanceGeometry.Resolve(player);
                    Check(geometry.Coverage==AppearanceCoverage.Exact&&geometry.Anchor(player.m_body.Position)==new Vector2(185.25f,214.75f),"Active Jing anchor preserves fractional coordinates");
                    HitboxResizer.Patches.PrefixPlayerEntityDraw.Width=11;
                    Check(AppearanceGeometry.Resolve(player).AnchorOffset==new Vector2(5,14),"Odd width follows the foreign integer-half rule");
                    using(var unknown=new OwnedPatches("unreviewed.visual"))
                    {
                        unknown.Add(typeof(PlayerEntity).GetMethod("Draw"),postfix:typeof(AppearanceTests).GetMethod("ForeignDraw",OwnedPatches.Members));
                        Check(AppearanceGeometry.Resolve(player).Coverage==AppearanceCoverage.Approximate,"Patch mutations invalidate exact coverage");
                    }
                    Check(AppearanceGeometry.Resolve(player).Coverage==AppearanceCoverage.Exact,"Removing unknown patches restores coverage");
                }
                Check(AppearanceGeometry.Resolve(player).AnchorOffset==new Vector2(9,26),"Unpatching restores native geometry even while foreign fields remain set");
                Sprite form=new TestSprite(),attachment=new TestSprite(),effect=new TestSprite(),latest=new TestSprite();
                using(var a=PlayerVisuals.Register(player,"test.form",VisualPhase.Form,s=>{Check(s==latest||s==original,"Form reads the native pose");return form;}))
                using(var b=PlayerVisuals.Register(player,"test.attachment",VisualPhase.Attachment,s=>{Check(s==form,"Attachments follow forms");return attachment;}))
                using(var c=PlayerVisuals.Register(player,"test.trail",VisualPhase.Behind,s=>{Check(s==attachment,"Effects follow the body");return effect;}))
                {
                    player.SetSprite(latest);
                    Check(PlayerAppearance.BaseSprite(player)==latest,"Bridge follows native pose changes");
                    Check(PlayerAppearance.Resolve(player).Sprite==attachment,"Body captures exclude trails");
                    Check(PlayerAppearance.Resolve(player,AppearanceStage.Complete).Sprite==effect,"Live presentation includes the trail");
                    using(var failed=PlayerVisuals.Register(player,"test.failure",VisualPhase.Front,s=>{throw new Exception("fixture");}))
                    {Check(PlayerAppearance.Resolve(player,AppearanceStage.Complete).Sprite==effect&&failed.LastError=="fixture","One failed contribution preserves the body");}
                }
                Check(PlayerAppearance.SpriteField.GetValue(player)==latest,"Last release restores the latest native pose");
            }
            int notifications=0,signals=0;
            using(PlayerAppearance.Subscribe(v=>{throw new Exception("fixture");}))
            using(PlayerAppearance.Subscribe(v=>notifications++))
            using(PlayerAppearance.SubscribeSignals(v=>{if(v.Player==player&&v.Delivery==PresentationDelivery.Restore)signals++;}))
            {PlayerAppearance.Publish();PlayerAppearance.Signal(player,"test/form",PresentationDelivery.Restore);}
            Check(notifications==1&&signals==1,"Publication isolates failures and signals retain actor/provenance");
            long before=CosmeticPlayback.Sequence;CosmeticPlayback.Record("live");
            using(CosmeticPlayback.Playback()){CosmeticPlayback.Record("replayed");}
            Check(CosmeticPlayback.ReadEvents(before).Length==1,"Playback cannot record its own cosmetic events");
            Console.WriteLine("[OK] Appearance: Jing/odd-width/fractional geometry, patch invalidation, stages, native pose restoration, failure isolation and playback provenance");
        }
    }
}
