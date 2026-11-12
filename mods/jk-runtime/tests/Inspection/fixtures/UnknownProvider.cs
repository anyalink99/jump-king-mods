using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.Level;
using JumpKing.Level.Sampler;
using Microsoft.Xna.Framework;

namespace UnknownProvider
{
    public sealed class DeclaredOneWay : IBlock
    {
        private readonly Rectangle rect;
        public bool Active=true;
        public int Reads;
        public DeclaredOneWay(Rectangle rectangle) { rect=rectangle; }
        public Rectangle GetRect() { return rect; }
        public BlockCollisionType Intersects(Rectangle box,out Rectangle overlap)
        { overlap=Rectangle.Intersect(box,rect); return rect.Intersects(box)?BlockCollisionType.Collision_NonBlocking:BlockCollisionType.NoCollision; }
        public static long Capture(IBlock block) { var value=(DeclaredOneWay)block; value.Reads++; return value.Active?1:0; }
    }
    public sealed class DeclaredOneWayBehaviour : IBlockBehaviour
    {
        public float BlockPriority { get { return 2; } }
        public bool IsPlayerOnBlock { get; set; }
        public float ModifyXVelocity(float value,BehaviourContext context) { return value; }
        public float ModifyYVelocity(float value,BehaviourContext context) { return value; }
        public float ModifyGravity(float value,BehaviourContext context) { return value; }
        public bool ExecuteBlockBehaviour(BehaviourContext context) { return true; }
        public bool AdditionalXCollisionCheck(AdvCollisionInfo info,BehaviourContext context) { return false; }
        public bool AdditionalYCollisionCheck(AdvCollisionInfo info,BehaviourContext context)
        {
            if(context.BodyComp.Velocity.Y<=0 || context.CollisionInfo.StartOfFrameCollisionInfo.IsCollidingWith<DeclaredOneWay>())return false;
            foreach(var block in info.GetCollidedBlocks<DeclaredOneWay>())if(((DeclaredOneWay)block).Active)return true;
            return false;
        }
    }
    public sealed class SpeedMedium : IBlockBehaviour
    {
        public float Factor=.37f;
        public int Calls,Reads;
        public float BlockPriority {get{return 2;}}
        public bool IsPlayerOnBlock {get;set;}
        private float Scale {get{Reads++;return Factor;}}
        [MethodImpl(MethodImplOptions.NoInlining)]
        public float ModifyXVelocity(float value,BehaviourContext context){Calls++;return value*Scale;}
        public float ModifyYVelocity(float value,BehaviourContext context){return value;}
        public float ModifyGravity(float value,BehaviourContext context){return value;}
        public bool ExecuteBlockBehaviour(BehaviourContext context){return true;}
        public bool AdditionalXCollisionCheck(AdvCollisionInfo info,BehaviourContext context){return false;}
        public bool AdditionalYCollisionCheck(AdvCollisionInfo info,BehaviourContext context){return false;}
    }
    public enum Form { Rest, Travel, Heavy }
    public sealed class Material : BoxBlock
    {
        public readonly int Parameter;
        public Material(Rectangle rect, int parameter) : base(rect) { Parameter = parameter; }
    }
    public sealed class OwnedMaterial : BoxBlock
    {
        public List<int> State = new List<int>();
        public OwnedMaterial(Rectangle rect) : base(rect) { }
    }
    public sealed class Factory : IBlockFactory
    {
        public static readonly Color Code = new Color(91, 192, 93);
        public static readonly HashSet<int> ScreenIndices = new HashSet<int>();
        public int Calls;
        public bool CanMakeBlock(Color code, JumpKing.Workshop.Level level) { return code == Code; }
        public bool IsSolidBlock(Color code) { return true; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IBlock GetBlock(Color code, Rectangle rect, JumpKing.Workshop.Level level, LevelTexture texture, int screen, int x, int y)
        { Calls++; return new Material(rect, 42); }
    }
    public static class Control
    {
        public static bool Held { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
        public static Form Mode { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool ReadHeld() { return Held; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static Form ReadMode() { return Mode; }
    }
    public sealed class Transition { public bool target; }
    public sealed class Behaviour : IBodyCompBehaviour
    {
        public readonly Transition transition = new Transition();
        public Form phase;
        public bool Held { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
        public bool ExecuteBehaviour(BehaviourContext context) { return true; }
    }
    public static class DormantSubsystem
    {
        public static bool State;
        static DormantSubsystem() { throw new InvalidOperationException("Discovery must not initialize dormant subsystems"); }
    }
    public class PortableZone : IBlock
    {
        private readonly Rectangle rectangle;
        public readonly int Parameter;
        public PortableZone(Rectangle value, int parameter) { rectangle = value; Parameter = parameter; }
        public Rectangle GetRect() { return rectangle; }
        public BlockCollisionType Intersects(Rectangle hitbox, out Rectangle overlap)
        { overlap = Rectangle.Intersect(rectangle, hitbox); return rectangle.Intersects(hitbox) ? BlockCollisionType.Collision_NonBlocking : BlockCollisionType.NoCollision; }
    }
    public sealed class PortableFactory : IBlockFactory
    {
        public static ulong LastMap = ulong.MaxValue;
        public static readonly Color Code = new Color(13, 91, 42);
        public static readonly HashSet<int> Screens = new HashSet<int>();
        public bool CanMakeBlock(Color colour, JumpKing.Workshop.Level level) { return colour.R == Code.R && colour.G == Code.G; }
        public bool IsSolidBlock(Color colour) { return false; }
        public IBlock GetBlock(Color colour, Rectangle rect, JumpKing.Workshop.Level level, LevelTexture texture, int screen, int x, int y)
        {
            if (LastMap != level.ID) LastMap = level.ID;
            Screens.Add(screen);
            if (colour.R == 13) return new PortableZone(rect, colour.B + 3);
            System.IO.File.WriteAllText("construction-must-not-write.txt", "bad");
            return null;
        }
    }
    public sealed class PortableHandler : IBlockBehaviour
    {
        public readonly Transition contact = new Transition();
        public readonly HashSet<int> contactScreens = new HashSet<int>();
        public static bool PreviousContact { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
        private readonly ICollisionQuery query;
        public PortableHandler(ICollisionQuery value) { query = value; }
        public float BlockPriority { get { return 2; } }
        public bool IsPlayerOnBlock { get; set; }
        public float ModifyXVelocity(float value, BehaviourContext context) { return value; }
        public float ModifyYVelocity(float value, BehaviourContext context) { return value; }
        public float ModifyGravity(float value, BehaviourContext context) { return IsPlayerOnBlock ? value * 3 : value; }
        public bool AdditionalXCollisionCheck(AdvCollisionInfo info, BehaviourContext context) { return false; }
        public bool AdditionalYCollisionCheck(AdvCollisionInfo info, BehaviourContext context) { return false; }
        public bool ExecuteBlockBehaviour(BehaviourContext context)
        { IsPlayerOnBlock = query.GetCollisionInfo(context.BodyComp.GetHitbox()).IsCollidingWith<PortableZone>(); return true; }
    }
    public static class Registration
    {
        public static int StartupCalls;
        public static PortableHandler Published;
        public static void Start(JumpKing.Player.BodyComp body, ICollisionQuery query)
        { StartupCalls++; body.RegisterBlockBehaviour(typeof(PortableZone), new PortableHandler(query)); }
        public static void Publish(JumpKing.Player.BodyComp body, ICollisionQuery query)
        { body.RegisterBlockBehaviour(typeof(PublishedZone), Published = new PortableHandler(query)); }
    }
    public sealed class PublishedZone : PortableZone
    { public PublishedZone(Rectangle rectangle) : base(rectangle, 1) { } }
    public sealed class StatefulConstructor
    { public StatefulConstructor() { Registration.StartupCalls++; } }
    public static class MenuProbe
    {
        public static int Calls;
        public static ProbeToggle Create(object factory, JumpKing.PauseMenu.GuiFormat format) { Calls++; return null; }
    }
    public sealed class ProbeToggle : JumpKing.PauseMenu.BT.Actions.IToggle
    {
        public ProbeToggle() : base(false) { }
        protected override void OnToggle() { }
        public override void Draw(int x, int y, bool selected) { }
        public override Point GetSize() { return Point.Zero; }
    }
}
