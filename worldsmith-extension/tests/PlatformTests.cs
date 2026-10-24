using System;
using System.Drawing;
using System.IO;
using System.Linq;
using MegaMappingExpansion;

namespace WorldsmithExtension
{
    internal static class PlatformTests
    {
        internal static void Run(string directory,Action<bool,string> check,Action<Action,string> fails)
        {
            string root=Path.Combine(directory,"platform-authoring");Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,"mapping_settings.xml"),"<MappingSettings version='1'><MapLayout version='2' source='atlas' screens='1' columns='1' rows='1' cellWidth='480' cellHeight='360'/></MappingSettings>");
            using(var image=new Bitmap(480,360))using(var graphics=Graphics.FromImage(image))
            {graphics.Clear(Color.Transparent);graphics.FillRectangle(new SolidBrush(Color.FromArgb(96,160,208)),120,280,48,8);image.Save(Path.Combine(root,"level+.png"));}
            var plan=PlatformAuthoring.Validate(root,PlatformAuthoring.Example,1);
            check(plan.Platforms.Count==2&&plan.Platforms.Single(p=>p.Id.StartsWith("paint-")).Parts.Count==1,"pixel marker rows merge into a single editable platform rectangle");
            PlatformAuthoring.Save(root,PlatformAuthoring.Example,1);string path=Path.Combine(root,MovingPlatformPlan.PathName),before=File.ReadAllText(path);
            fails(()=>PlatformAuthoring.Save(root,before.Replace("speed=\"40\"","speed=\"NaN\""),1),"invalid platform edits are rejected before saving");
            check(File.ReadAllText(path)==before,"a failed platform save preserves source bytes");
            PlatformAuthoring.Save(root,before.Replace("speed=\"40\"","speed=\"50\""),1);check(File.ReadAllText(path+".worldsmith-backup")==before,"platform settings keep an atomic rollback copy");
            fails(()=>PlatformAuthoring.Validate(root,before.Replace("distance=\"-96\"","distance=\"-1000\""),1),"editor validation catches routes leaving the map");
            fails(()=>MovingPlatformPlan.Parse("<!DOCTYPE x [<!ENTITY v 'x'>]><MovingPlatforms version='1'>&v;</MovingPlatforms>",1),"the platform parser prohibits document types and entity expansion");
            var motion=new PlatformMotionPlayer(plan.Motions["lift"]);motion.Advance(.75,false);check(Math.Abs(motion.Position.Y+10)<1e-8,"editor preview includes the authored initial wait and speed");
        }
    }
}
