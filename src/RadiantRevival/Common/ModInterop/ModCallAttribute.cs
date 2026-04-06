using System;
using JetBrains.Annotations;

namespace RadiantRevival.Common;

[MeansImplicitUse()]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModCallAttribute(params string[] nameAliases) : Attribute
{
    public string[] NameAliases { get; } = nameAliases;
}
