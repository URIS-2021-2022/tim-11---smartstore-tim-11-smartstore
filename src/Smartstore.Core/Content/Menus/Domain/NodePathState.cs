using System;

namespace Smartstore.Core.Content.Menus
{
    [Flags]
    public enum NodePathState
    {
        None = 0,
        Parent = 1,
        Expanded = 2,
        Selected = 4
    }
}