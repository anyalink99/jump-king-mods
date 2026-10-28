using System;
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]

namespace MegaMappingExpansion.Api
{
    /// <summary>Borrow a game-owned Texture2D and optional audio controls. The provider retains ownership.</summary>
    public interface IMappingMedia
    {
        IDisposable Register(string source,object texture,Action<bool> pause,Action stop);
    }
}
