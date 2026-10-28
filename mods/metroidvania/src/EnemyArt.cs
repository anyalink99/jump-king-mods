using System;
using System.Collections.Generic;
using JumpKing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Metroidvania
{
    internal sealed class EnemyArt : IDisposable
    {
        private readonly EnemyDefinition[] definitions=new EnemyDefinition[33];
        private readonly Texture2D[] atlases=new Texture2D[33];
        internal EnemyDefinition Definition {get;private set;}
        internal EnemyArt(){ContentCatalog.Prepare();Definition=Prepare(ContentCatalog.DefaultEnemy);}
        internal EnemyDefinition Prepare(string id)
        {
            var recipe=ContentCatalog.Enemy(id);if(recipe==null)throw new InvalidOperationException("Unknown enemy: "+id);
            if(definitions[recipe.Index]!=null)return definitions[recipe.Index];var definition=EnemyDefinition.Load(id);Texture2D atlas;
            using(var source=typeof(EnemyArt).Assembly.GetManifestResourceStream(recipe.Atlas)){
                if(source==null)throw new InvalidOperationException("Missing enemy atlas: "+id);atlas=Texture2D.FromStream(Game1.spriteBatch.GraphicsDevice,source);
            }
            if(atlas.Width!=recipe.Cell*recipe.Columns||atlas.Height<recipe.Cell*((definition.FrameCount+recipe.Columns-1)/recipe.Columns)){atlas.Dispose();throw new InvalidOperationException("Invalid enemy atlas: "+id);}
            definitions[recipe.Index]=definition;atlases[recipe.Index]=atlas;return definition;
        }
        internal EnemyDefinition[] Prepared()
        {var result=new List<EnemyDefinition>{Definition};foreach(var value in definitions)if(value!=null&&!ReferenceEquals(value,Definition))result.Add(value);return result.ToArray();}
        internal EnemyDefinition For(FighterState actor){return definitions[actor.Archetype];}
        internal void Draw(FighterState actor,Vector2 anchor,Color tint)
        {
            var definition=For(actor);var recipe=definition.Recipe;int frame=definition.FrameIndex(actor);var rect=new Rectangle(frame%recipe.Columns*recipe.Cell,frame/recipe.Columns*recipe.Cell,recipe.Cell,recipe.Cell);
            Game1.spriteBatch.Draw(atlases[actor.Archetype],anchor,rect,tint,0,new Vector2(actor.Facing>0?recipe.Cell-recipe.PivotX:recipe.PivotX,recipe.PivotY),1,actor.Facing>0?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
        }
        public void Dispose(){foreach(var atlas in atlases)if(atlas!=null)atlas.Dispose();}
    }
}
